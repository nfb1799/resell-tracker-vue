import { defineStore } from 'pinia'
import { computed, ref, shallowRef } from 'vue'
import { ApiError, itemsApi, type ItemDto } from '@/api'
import type { DonationRequestDto, ItemRequestDto, SaleRequestDto } from '@/api/types'
import { getLocalDateString } from '@/domain/dates'
import { fromDto, toRequest, type Item, type ItemFields } from '@/domain/item'
import { toDollars, type Cents } from '@/domain/money'
import type { SaleFigures } from '@/domain/profit'
import { conflictRows, mergeEdit } from '@/offline/conflicts'
import * as offline from '@/offline/db'
import type { OpKind, OpPayload, QueuedOp } from '@/offline/db'
import { applyPending } from '@/offline/optimistic'
import { httpSyncApi, isNetworkError, sendOp, type SyncApi } from '@/offline/sync'
import { useAuthStore } from './auth'
import { useConnectionStore } from './connection'
import { useSettingsStore } from './settings'
import { useStatsStore } from './stats'
import { useToastStore } from './toast'

export interface SaleInput extends SaleFigures {
  listedFor: Cents
  date: string
}

export interface DonationInput {
  date: string
  org: string
  receiptValue: Cents | null
}

const blobToDataUrl = (blob: Blob) =>
  new Promise<string>((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as string)
    reader.onerror = () => reject(reader.error)
    reader.readAsDataURL(blob)
  })

/**
 * The signed-in user's whole inventory, kept in step with every change and
 * available offline.
 *
 * Two things make up what shows: `confirmed`, the server's items as last seen
 * (also saved on the device, so the app opens with no connection), and `ops`, the
 * outbox of changes the server hasn't had yet, laid on top. Online, a change goes
 * straight to the server as before; offline, or behind changes already waiting, it
 * joins the outbox and `flush` sends it when the connection is back.
 *
 * Filtering and sorting happen on the client, as in the original: it is what
 * makes search instant, and it is what works offline.
 */
export const useItemsStore = defineStore('items', () => {
  // Shallow on purpose: both are only ever replaced, never mutated, and their
  // contents go into IndexedDB, which can't store Vue's reactive proxies.
  const confirmed = shallowRef<ItemDto[]>([])
  const ops = shallowRef<QueuedOp[]>([])
  const loaded = ref(false)
  /** A way to reach the server; swapped for a fake in tests. */
  let api: SyncApi = httpSyncApi

  const auth = useAuthStore()
  const connection = useConnectionStore()
  const settings = useSettingsStore()
  const toast = useToastStore()

  const userId = () => auth.me?.id ?? ''

  const view = computed(() => applyPending(confirmed.value, ops.value, settings.fees))
  const issues = computed(() => ops.value.filter((o) => o.state !== 'pending'))
  const pendingCount = computed(() => ops.value.filter((o) => o.state === 'pending').length)

  const items = computed<Item[]>(() => {
    const troubled = new Set(issues.value.map((o) => o.itemId))
    return view.value.items.map((dto) => ({
      ...fromDto(dto),
      unsynced: view.value.unsynced.has(dto.id),
      syncIssue: troubled.has(dto.id),
    }))
  })
  const byId = computed(() => new Map(items.value.map((i) => [i.id, i])))

  let saveTimer: ReturnType<typeof setTimeout> | undefined
  function persist() {
    const id = userId()
    clearTimeout(saveTimer)
    saveTimer = setTimeout(() => void offline.writeSnapshot(id, 'items', confirmed.value).catch(() => {}), 300)
  }

  /** Puts the server's copy of an item in place (or takes it out), newest-first for new ones. */
  function confirm(dto: ItemDto | null, itemId: string) {
    const known = confirmed.value.some((i) => i.id === itemId)
    if (!dto) confirmed.value = confirmed.value.filter((i) => i.id !== itemId)
    else if (known) confirmed.value = confirmed.value.map((i) => (i.id === itemId ? dto : i))
    else confirmed.value = [dto, ...confirmed.value]
    useStatsStore().invalidate()
    persist()
  }

  async function load() {
    const id = userId()
    ops.value = await offline.queuedOps(id).catch(() => [])
    if (!loaded.value) {
      const snapshot = await offline.readSnapshot<ItemDto[]>(id, 'items').catch(() => undefined)
      if (snapshot) {
        confirmed.value = snapshot
        loaded.value = true
      }
    }
    try {
      confirmed.value = await itemsApi.list()
      loaded.value = true
      connection.markOnline()
      persist()
      void flush()
    } catch (error) {
      if (!isNetworkError(error)) throw error
      // Offline: carry on from what the device has.
      connection.markOffline()
      loaded.value = true
    }
  }

  function clear() {
    confirmed.value = []
    ops.value = []
    loaded.value = false
  }

  // ── changes ───────────────────────────────────────────────────────────────

  async function queue(op: QueuedOp): Promise<Item | undefined> {
    ops.value = [...ops.value, await offline.enqueue(op)]
    if (connection.online) void flush()
    return byId.value.get(op.itemId)
  }

  /**
   * Makes one change. Online with nothing already waiting, it goes straight to the
   * server so the form hears about any problem; a stale version reloads the list
   * and says so. With no connection it joins the outbox and shows as done.
   */
  async function change(kind: OpKind, itemId: string, item: Item | null, payload: OpPayload): Promise<Item> {
    const op: QueuedOp = {
      userId: userId(),
      itemId,
      kind,
      payload,
      baseVersion: item?.version || null,
      label: item?.title ?? (payload as ItemRequestDto | null)?.title ?? 'Item',
      createdAt: new Date().toISOString(),
      state: 'pending',
      ...(kind === 'update' ? { base: view.value.items.find((i) => i.id === itemId) } : {}),
    }

    const waiting = ops.value.length > 0
    if (connection.online && !waiting) {
      try {
        const dto = await api.send(op, op.baseVersion ?? '')
        confirm(dto, itemId)
        return byId.value.get(itemId) ?? (dto ? fromDto(dto) : item!)
      } catch (error) {
        if (!isNetworkError(error)) {
          if (error instanceof ApiError && error.isStale) {
            toast.show('That item changed on another device. Showing the latest; try again.', 'error')
            await load()
          }
          throw error
        }
        connection.markOffline()
      }
    }

    const optimistic = await queue(op)
    toast.show('Saved on this device. It will sync when you are back online.', 'info')
    return optimistic ?? item!
  }

  const create = (fields: ItemFields) => {
    const id = crypto.randomUUID()
    return change('create', id, null, toRequest(fields, id))
  }

  const update = (item: Item, fields: ItemFields) => change('update', item.id, item, toRequest(fields))

  const sell = (item: Item, sale: SaleInput) =>
    change('sell', item.id, item, {
      platform: sale.platform,
      listedFor: toDollars(sale.listedFor),
      price: toDollars(sale.price),
      payout: sale.payout === null ? null : toDollars(sale.payout),
      shippingCharged: toDollars(sale.shippingCharged),
      shippingCost: toDollars(sale.shippingCost),
      otherCosts: toDollars(sale.otherCosts),
      date: sale.date,
    } satisfies SaleRequestDto)

  const undoSale = (item: Item) => change('undoSale', item.id, item, null)

  const donate = (item: Item, donation: DonationInput) =>
    change('donate', item.id, item, {
      ...donation,
      receiptValue: donation.receiptValue === null ? null : toDollars(donation.receiptValue),
    } satisfies DonationRequestDto)

  const undoDonation = (item: Item) => change('undoDonation', item.id, item, null)

  const setPhoto = async (item: Item, thumbnail: Blob, full: Blob) =>
    change('setPhoto', item.id, item, { thumbnail, full, thumbnailUrl: await blobToDataUrl(thumbnail) })

  const removePhoto = (item: Item) => change('removePhoto', item.id, item, null)

  async function remove(item: Item) {
    await change('delete', item.id, item, null)
  }

  /** Sends pasted rows to the server, which checks them again and adds the good ones. Needs a connection. */
  async function importRows(rows: unknown[]) {
    const result = await itemsApi.import(rows, getLocalDateString())
    const added = new Map<number, Item>()
    for (const row of result.rows) {
      if (!row.item) continue
      confirm(row.item, row.item.id)
      added.set(row.rowNumber, byId.value.get(row.item.id)!)
    }
    return { ...result, added }
  }

  // ── syncing the outbox ────────────────────────────────────────────────────

  async function drop(op: QueuedOp) {
    await offline.removeOp(op.seq!)
    ops.value = ops.value.filter((o) => o.seq !== op.seq)
  }

  async function saveQueued(op: QueuedOp) {
    await offline.saveOp(op)
    ops.value = ops.value.map((o) => (o.seq === op.seq ? op : o))
  }

  /** Points the changes still queued for an item at a newer version of it. */
  async function rebase(itemId: string, version: string, after: number) {
    for (const later of ops.value.filter((o) => o.itemId === itemId && o.seq! > after)) {
      await saveQueued({ ...later, baseVersion: version })
    }
  }

  let flushing: Promise<void> | null = null

  /** Sends whatever is waiting, oldest first. Safe to call any time; runs one at a time. */
  function flush(): Promise<void> {
    flushing ??= run().finally(() => (flushing = null))
    return flushing
  }

  async function run() {
    const hadPending = pendingCount.value > 0
    const blocked = new Set(issues.value.map((o) => o.itemId))

    // Walk the queue as it stood when the flush began, but read each change
    // afresh: an earlier one syncing re-points later ones at the new version.
    for (const seq of ops.value.map((o) => o.seq!)) {
      const op = ops.value.find((o) => o.seq === seq)
      if (!op || op.state !== 'pending' || blocked.has(op.itemId)) continue
      const result = await sendOp(op, op.baseVersion ?? '', api)

      if (result.kind === 'offline') {
        connection.markOffline()
        return
      }
      if (result.kind === 'signedOut') {
        auth.forget()
        return
      }
      connection.markOnline()

      if (result.kind === 'synced') {
        confirm(result.item, op.itemId)
        if (result.item) await rebase(op.itemId, result.item.version, op.seq!)
        await drop(op)
      } else if (result.kind === 'conflict') {
        confirm(result.theirs, op.itemId)
        if (conflictRows(op, result.theirs, settings.currency).length === 0) {
          // The server already has exactly this change.
          await rebase(op.itemId, result.theirs.version, op.seq!)
          await drop(op)
        } else {
          await saveQueued({ ...op, state: 'conflict', theirs: result.theirs })
          blocked.add(op.itemId)
        }
      } else {
        await saveQueued({ ...op, state: 'failed', error: result.error })
        blocked.add(op.itemId)
      }
    }

    if (hadPending && pendingCount.value === 0 && issues.value.length === 0) toast.show('Back online — everything synced', 'success')
  }

  // ── deciding conflicts ────────────────────────────────────────────────────

  /** Apply my change on top of what the server has now. */
  async function keepMine(op: QueuedOp) {
    const theirs = op.theirs!
    const payload = op.kind === 'update' ? mergeEdit(op, theirs) : op.payload
    await saveQueued({ ...op, state: 'pending', payload, base: theirs, baseVersion: theirs.version, theirs: undefined, error: undefined })
    await flush()
  }

  /**
   * Drops my change: "Keep theirs" on a conflict, "Discard" on a change the server
   * refused. Later changes to the item then go on top of the server's version; if
   * the item isn't on the server at all (its create was dropped, or it was deleted
   * elsewhere), nothing else queued for it can land, so those go too.
   */
  async function discard(op: QueuedOp) {
    await drop(op)
    const current = op.theirs ?? confirmed.value.find((i) => i.id === op.itemId)
    if (current) await rebase(op.itemId, current.version, op.seq!)
    else for (const later of ops.value.filter((o) => o.itemId === op.itemId)) await drop(later)
    await flush()
  }

  connection.onReconnect(() => void flush())

  return {
    items,
    loaded,
    byId,
    ops,
    issues,
    pendingCount,
    load,
    clear,
    flush,
    keepMine,
    discard,
    create,
    update,
    sell,
    undoSale,
    donate,
    undoDonation,
    setPhoto,
    removePhoto,
    importRows,
    remove,
    /** For tests: talk to a fake server. */
    useApi: (next: SyncApi) => {
      api = next
    },
  }
})
