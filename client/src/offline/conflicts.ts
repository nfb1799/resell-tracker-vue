// What a conflicting change would do, field by field, next to what the server has
// now: the comparison the conflict sheet shows before the user picks a side. Only
// fields that actually differ are listed; none at all means the server already has
// this change, so it isn't a conflict (a replay whose first response was lost).

import type { DonationRequestDto, ItemDto, ItemRequestDto, SaleRequestDto } from '@/api/types'
import { STATUS_LABELS } from '@/domain/lifecycle'
import { formatMoney, fromDollars } from '@/domain/money'
import { platformLabel } from '@/domain/platforms'
import { formatDate } from '@/lib/format'
import type { QueuedOp } from './db'

export interface ConflictRow {
  field: string
  yours: string
  theirs: string
}

export const CHANGE_LABELS: Record<QueuedOp['kind'], string> = {
  create: 'Added',
  update: 'Edited',
  sell: 'Logged a sale',
  undoSale: 'Undid the sale',
  donate: 'Marked donated',
  undoDonation: 'Undid the donation',
  setPhoto: 'Changed the photo',
  removePhoto: 'Removed the photo',
  delete: 'Deleted',
}

/** The fields an edit sends. Every edit sends all of them, changed or not. */
const EDIT_FIELDS = [
  ['title', 'Title'],
  ['brand', 'Brand'],
  ['size', 'Size'],
  ['category', 'Category'],
  ['condition', 'Condition'],
  ['cost', 'Cost'],
  ['source', 'Sourced from'],
  ['acquiredDate', 'Acquired'],
  ['platforms', 'Listed on'],
  ['listPrice', 'Asking price'],
  ['listedDate', 'Listed date'],
  ['notes', 'Notes'],
] as const satisfies readonly (readonly [keyof ItemRequestDto & keyof ItemDto, string])[]

type EditField = (typeof EDIT_FIELDS)[number][0]

const same = (a: unknown, b: unknown) =>
  Array.isArray(a) && Array.isArray(b)
    ? a.join('+') === b.join('+')
    : (typeof a === 'string' ? a.trim() : (a ?? null)) === (typeof b === 'string' ? b.trim() : (b ?? null))

/**
 * The fields an edit actually changed: those that differ from the item as it
 * stood when the edit was made. Without that snapshot, every field counts.
 */
function editedFields(op: QueuedOp): EditField[] {
  const mine = op.payload as ItemRequestDto
  return EDIT_FIELDS.map(([key]) => key).filter((key) => !op.base || !same(mine[key], op.base[key]))
}

/**
 * "Keep mine" for an edit: the server's current item with only the fields I
 * changed laid on top, so keeping my new title doesn't quietly undo a price
 * someone else changed.
 */
export function mergeEdit(op: QueuedOp, theirs: ItemDto): ItemRequestDto {
  const mine = op.payload as ItemRequestDto
  const merged: ItemRequestDto = {
    title: theirs.title,
    brand: theirs.brand,
    category: theirs.category,
    size: theirs.size,
    condition: theirs.condition,
    notes: theirs.notes,
    cost: theirs.cost,
    source: theirs.source,
    acquiredDate: theirs.acquiredDate,
    platforms: [...theirs.platforms],
    listPrice: theirs.listPrice,
    listedDate: theirs.listedDate,
  }
  for (const key of editedFields(op)) Object.assign(merged, { [key]: key === 'platforms' ? [...mine.platforms] : mine[key] })
  return merged
}

export function conflictRows(op: QueuedOp, theirs: ItemDto, currency: string): ConflictRow[] {
  const money = (n: number | null | undefined) => (n === null || n === undefined ? '—' : formatMoney(fromDollars(n), currency))
  const text = (s: string | null | undefined) => (s ? s.trim() : '—')
  const date = (d: string | null | undefined) => (d ? formatDate(d) : '—')
  const platforms = (ids: readonly string[]) => (ids.length ? ids.map(platformLabel).join(' + ') : 'not listed')
  const status = (s: ItemDto['status']) => STATUS_LABELS[s]

  const rows: ConflictRow[] = []
  const compare = (field: string, yours: string, theirsValue: string) => {
    if (yours !== theirsValue) rows.push({ field, yours, theirs: theirsValue })
  }

  switch (op.kind) {
    case 'update': {
      // Only what this edit changed; fields it carried along unchanged aren't mine to fight over.
      const f = op.payload as ItemRequestDto
      const show = (key: EditField, item: ItemRequestDto | ItemDto): string => {
        if (key === 'platforms') return platforms(item.platforms)
        if (key === 'cost' || key === 'listPrice') return money(item[key])
        if (key === 'acquiredDate' || key === 'listedDate') return date(item[key])
        return text(item[key])
      }
      for (const key of editedFields(op)) {
        const label = EDIT_FIELDS.find(([k]) => k === key)![1]
        compare(label, show(key, f), show(key, theirs))
      }
      break
    }
    case 'sell': {
      const s = op.payload as SaleRequestDto
      const t = theirs.sale
      compare('Status', status('sold'), status(theirs.status))
      compare('Sold on', platformLabel(s.platform), t ? platformLabel(t.platform) : '—')
      compare('Offer accepted', money(s.price), money(t?.price))
      compare('Payout', money(s.payout), money(t?.payout))
      compare('Shipping buyer paid', money(s.shippingCharged), money(t?.shippingCharged))
      compare('Shipping you paid', money(s.shippingCost), money(t?.shippingCost))
      compare('Other costs', money(s.otherCosts), money(t?.otherCosts))
      compare('Sale date', date(s.date), date(t?.date))
      break
    }
    case 'donate': {
      const d = op.payload as DonationRequestDto
      const t = theirs.donation
      compare('Status', status('donated'), status(theirs.status))
      compare('Donated to', text(d.org), text(t?.org))
      compare('Date donated', date(d.date), date(t?.date))
      compare('Receipt value', money(d.receiptValue), money(t?.receiptValue))
      break
    }
    case 'undoSale':
    case 'undoDonation': {
      // Already back on hand means the server has this change; otherwise it's
      // still sold (or donated) but was changed elsewhere in the meantime.
      const ended = op.kind === 'undoSale' ? 'sold' : 'donated'
      if (theirs.status === ended) rows.push({ field: 'Status', yours: 'back in stock', theirs: `${status(ended)}, changed since` })
      break
    }
    case 'setPhoto':
      rows.push({ field: 'Photo', yours: 'your new photo', theirs: theirs.thumbnail ? 'a photo set on another device' : 'no photo' })
      break
    case 'removePhoto':
      if (theirs.thumbnail) rows.push({ field: 'Photo', yours: 'no photo', theirs: 'a photo' })
      break
    case 'delete':
      rows.push({ field: 'Item', yours: 'deleted', theirs: 'changed on another device since' })
      break
    case 'create':
      break
  }
  return rows
}
