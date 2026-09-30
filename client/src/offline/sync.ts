// Sends queued changes to the server, in the order they were made.
//
// Every change carries the item version it was made against, so the server can
// refuse one that would overwrite someone else's newer edit (412). That change is
// then held as a conflict for the user to decide, along with anything queued after
// it for the same item; changes to other items carry on.
//
// Replays are safe. New items carry an id made on the device, so a create the
// server already has (the response was lost) comes back 409 and counts as done;
// and a change the server already has shows up as a "conflict" with nothing
// actually different, which counts as done too (see conflicts.ts).

import { ApiError, itemsApi } from '@/api'
import type { DonationRequestDto, ItemDto, ItemRequestDto, SaleRequestDto } from '@/api/types'
import type { PhotoPayload, QueuedOp } from './db'

export type SendResult =
  | { kind: 'synced'; item: ItemDto | null }
  | { kind: 'conflict'; theirs: ItemDto }
  | { kind: 'failed'; error: string }
  | { kind: 'offline' }
  | { kind: 'signedOut' }

export interface SyncApi {
  send(op: QueuedOp, version: string): Promise<ItemDto | null>
  /** The server's current copy, or null if it no longer exists. */
  get(id: string): Promise<ItemDto | null>
}

const GONE = 'This item was deleted on another device.'

/** No connection, or nothing answering behind the proxy. */
export function isNetworkError(error: unknown): boolean {
  return error instanceof TypeError || (error instanceof ApiError && [0, 502, 503, 504].includes(error.status))
}

export const httpSyncApi: SyncApi = {
  async send(op, version) {
    const id = op.itemId
    switch (op.kind) {
      case 'create':
        return itemsApi.create(op.payload as ItemRequestDto)
      case 'update':
        return itemsApi.update(id, version, op.payload as ItemRequestDto)
      case 'sell':
        return itemsApi.sell(id, version, op.payload as SaleRequestDto)
      case 'undoSale':
        return itemsApi.undoSale(id, version)
      case 'donate':
        return itemsApi.donate(id, version, op.payload as DonationRequestDto)
      case 'undoDonation':
        return itemsApi.undoDonation(id, version)
      case 'setPhoto': {
        const p = op.payload as PhotoPayload
        return itemsApi.setPhoto(id, version, p.thumbnail, p.full)
      }
      case 'removePhoto':
        return itemsApi.removePhoto(id, version)
      case 'delete':
        await itemsApi.remove(id, version)
        return null
    }
  },
  async get(id) {
    try {
      return await itemsApi.get(id)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null
      throw error
    }
  },
}

/** Sends one change and says what became of it. */
export async function sendOp(op: QueuedOp, version: string, api: SyncApi): Promise<SendResult> {
  try {
    return { kind: 'synced', item: await api.send(op, version) }
  } catch (error) {
    if (isNetworkError(error)) return { kind: 'offline' }
    if (!(error instanceof ApiError)) return { kind: 'failed', error: 'Something went wrong sending this change.' }

    if (error.status === 401) return { kind: 'signedOut' }

    try {
      if (op.kind === 'create' && error.status === 409) {
        // Already there from an earlier attempt whose response was lost.
        const existing = await api.get(op.itemId)
        return existing ? { kind: 'synced', item: existing } : { kind: 'failed', error: error.message }
      }
      if (error.status === 404) {
        return op.kind === 'delete' ? { kind: 'synced', item: null } : { kind: 'failed', error: GONE }
      }
      if (error.status === 412) {
        const theirs = await api.get(op.itemId)
        if (!theirs) return op.kind === 'delete' ? { kind: 'synced', item: null } : { kind: 'failed', error: GONE }
        return { kind: 'conflict', theirs }
      }
    } catch (inner) {
      if (isNetworkError(inner)) return { kind: 'offline' }
      throw inner
    }

    // A change the server won't take at all (a sale on a donated item, say).
    return { kind: 'failed', error: error.message }
  }
}
