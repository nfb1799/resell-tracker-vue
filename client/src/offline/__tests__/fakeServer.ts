// An in-memory stand-in for the items API, with the real API's concurrency rules:
// every change names the version it was made against, a stale one is refused
// with 412, a create reusing an id with 409, and every change bumps the version.

import { ApiError } from '@/api'
import type { DonationRequestDto, ItemDto, ItemRequestDto, SaleRequestDto } from '@/api/types'
import { itemDto } from '@/__tests__/fixtures'
import type { PhotoPayload, QueuedOp } from '../db'
import type { SyncApi } from '../sync'

export class FakeServer implements SyncApi {
  readonly items = new Map<string, ItemDto>()
  readonly received: { kind: QueuedOp['kind']; itemId: string; version: string }[] = []
  offline = false
  private counter = 0

  seed(overrides: Partial<ItemDto>): ItemDto {
    const item = itemDto({ ...overrides, version: this.nextVersion() })
    this.items.set(item.id, item)
    return item
  }

  /** Someone else changes an item: the version moves on. */
  editElsewhere(id: string, changes: Partial<ItemDto>): ItemDto {
    const next = { ...this.items.get(id)!, ...changes, version: this.nextVersion() }
    this.items.set(id, next)
    return next
  }

  async get(id: string) {
    if (this.offline) throw new TypeError('Failed to fetch')
    return this.items.get(id) ?? null
  }

  async send(op: QueuedOp, version: string): Promise<ItemDto | null> {
    if (this.offline) throw new TypeError('Failed to fetch')
    this.received.push({ kind: op.kind, itemId: op.itemId, version })

    if (op.kind === 'create') {
      if (this.items.has(op.itemId)) throw new ApiError(409, 'Duplicate item', 'An item with that id already exists.')
      const f = op.payload as ItemRequestDto
      return this.save(itemDto({ ...f, id: op.itemId, status: f.platforms.length ? 'listed' : 'inventory' }))
    }

    const current = this.items.get(op.itemId)
    if (!current) throw new ApiError(404, 'Not found', 'No item with that id.')
    if (!version) throw new ApiError(428, 'Version required', 'Send the version.')
    if (version !== current.version) throw new ApiError(412, 'Stale version', 'The item changed since you loaded it.')

    switch (op.kind) {
      case 'update':
        return this.save({ ...current, ...(op.payload as ItemRequestDto) })
      case 'sell':
        if (current.status === 'donated') throw new ApiError(409, 'Not allowed', "A donated item can't be sold. Undo the donation first.")
        return this.save({ ...current, status: 'sold', sale: { ...(op.payload as SaleRequestDto) } })
      case 'undoSale':
        return this.save({ ...current, status: current.platforms.length ? 'listed' : 'inventory', sale: null })
      case 'donate':
        return this.save({ ...current, status: 'donated', donation: { ...(op.payload as DonationRequestDto) } })
      case 'undoDonation':
        return this.save({ ...current, status: current.platforms.length ? 'listed' : 'inventory', donation: null })
      case 'setPhoto':
        return this.save({ ...current, thumbnail: (op.payload as PhotoPayload).thumbnailUrl })
      case 'removePhoto':
        return this.save({ ...current, thumbnail: null })
      case 'delete':
        this.items.delete(op.itemId)
        return null
    }
  }

  private save(item: ItemDto): ItemDto {
    const saved = { ...item, version: this.nextVersion() }
    this.items.set(saved.id, saved)
    return saved
  }

  private nextVersion() {
    return `"v${++this.counter}"`
  }
}
