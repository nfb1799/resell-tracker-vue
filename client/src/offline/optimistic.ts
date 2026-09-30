// What an item looks like once a queued change is applied, before the server has
// seen it. Pure, and built on the same client rules the forms use (lifecycle,
// profit), so an offline sale shows the same figures the server will compute.
// Works on the API's item shape (dollars), the shape the snapshot stores.

import type { DonationRequestDto, ItemDto, ItemRequestDto, SaleRequestDto } from '@/api/types'
import { getLocalDateString } from '@/domain/dates'
import { applyPlatforms, isOnHand, statusAfterUndo } from '@/domain/lifecycle'
import { fromDollars, toDollars } from '@/domain/money'
import type { FeeSettings } from '@/domain/platforms'
import { computeProfit, projectedNet } from '@/domain/profit'
import type { PhotoPayload, QueuedOp } from './db'

const dollars = (n: number | null) => (n === null ? null : fromDollars(n))

function withFigures(item: ItemDto, fees: FeeSettings): ItemDto {
  const cost = fromDollars(item.cost)
  const p =
    item.status === 'sold' && item.sale
      ? computeProfit(
          cost,
          {
            platform: item.sale.platform,
            price: fromDollars(item.sale.price),
            shippingCharged: fromDollars(item.sale.shippingCharged),
            payout: dollars(item.sale.payout),
            shippingCost: fromDollars(item.sale.shippingCost),
            otherCosts: fromDollars(item.sale.otherCosts),
          },
          fees,
        )
      : null
  const projected = isOnHand(item.status)
    ? projectedNet({ listPrice: dollars(item.listPrice), cost, platforms: item.platforms }, fees)
    : null

  return {
    ...item,
    profit: p && {
      gross: toDollars(p.gross),
      payout: toDollars(p.payout),
      fees: toDollars(p.fees),
      cogs: toDollars(p.cogs),
      shippingCost: toDollars(p.shippingCost),
      otherCosts: toDollars(p.otherCosts),
      costs: toDollars(p.costs),
      net: toDollars(p.net),
      margin: p.margin,
      roi: p.roi,
      feesEstimated: p.feesEstimated,
    },
    projectedNet: projected === null ? null : toDollars(projected),
  }
}

function withFields(base: ItemDto, fields: ItemRequestDto, today: string): ItemDto {
  const listing = applyPlatforms(base.status, fields.platforms, fields.listedDate, today)
  return {
    ...base,
    title: fields.title.trim(),
    brand: fields.brand.trim(),
    category: fields.category.trim(),
    size: fields.size.trim(),
    condition: fields.condition,
    notes: fields.notes.trim(),
    cost: fields.cost,
    source: fields.source.trim(),
    acquiredDate: fields.acquiredDate,
    platforms: [...new Set(fields.platforms)],
    listPrice: fields.listPrice,
    status: listing.status,
    listedDate: listing.listedDate,
  }
}

function blank(id: string, now: string): ItemDto {
  return {
    id,
    status: 'inventory',
    title: '',
    brand: '',
    category: '',
    size: '',
    condition: '',
    notes: '',
    cost: 0,
    source: '',
    acquiredDate: null,
    platforms: [],
    listPrice: null,
    listedDate: null,
    thumbnail: null,
    sale: null,
    donation: null,
    profit: null,
    projectedNet: null,
    daysListed: null,
    createdAt: now,
    updatedAt: now,
    version: '',
  }
}

/** One queued change applied to one item; null when the change removes it. */
export function applyOp(item: ItemDto | undefined, op: QueuedOp, fees: FeeSettings, today = getLocalDateString()): ItemDto | null {
  if (op.kind === 'create') return withFigures(withFields(blank(op.itemId, op.createdAt), op.payload as ItemRequestDto, today), fees)
  if (!item) return null // a change to something no longer here

  switch (op.kind) {
    case 'update':
      return withFigures(withFields(item, op.payload as ItemRequestDto, today), fees)
    case 'sell': {
      const s = op.payload as SaleRequestDto
      return withFigures({ ...item, status: 'sold', sale: { ...s } }, fees)
    }
    case 'undoSale':
      return withFigures({ ...item, status: statusAfterUndo(item.platforms), sale: null }, fees)
    case 'donate': {
      const d = op.payload as DonationRequestDto
      return withFigures({ ...item, status: 'donated', donation: { ...d, org: d.org.trim() } }, fees)
    }
    case 'undoDonation':
      return withFigures({ ...item, status: statusAfterUndo(item.platforms), donation: null }, fees)
    case 'setPhoto':
      return { ...item, thumbnail: (op.payload as PhotoPayload).thumbnailUrl }
    case 'removePhoto':
      return { ...item, thumbnail: null }
    case 'delete':
      return null
  }
}

/**
 * The server's items with every still-pending change laid on top, newest first as
 * the server orders them. Changes waiting on a decision (conflicts, refusals) are
 * not applied: what shows is what the server has until the user chooses.
 */
export function applyPending(confirmed: readonly ItemDto[], ops: readonly QueuedOp[], fees: FeeSettings): { items: ItemDto[]; unsynced: Set<string> } {
  const byId = new Map(confirmed.map((i) => [i.id, i]))
  const unsynced = new Set<string>()
  for (const op of ops) {
    if (op.state !== 'pending') continue
    const next = applyOp(byId.get(op.itemId), op, fees)
    if (next) byId.set(op.itemId, next)
    else byId.delete(op.itemId)
    unsynced.add(op.itemId)
  }
  const items = [...byId.values()].sort((a, b) => b.createdAt.localeCompare(a.createdAt))
  return { items, unsynced }
}
