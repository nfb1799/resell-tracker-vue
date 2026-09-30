import { describe, it, expect } from 'vitest'
import { itemDto } from '@/__tests__/fixtures'
import type { ItemRequestDto } from '@/api/types'
import { withFeeDefaults } from '@/domain/platforms'
import type { QueuedOp } from '../db'
import { applyOp, applyPending } from '../optimistic'

const fees = withFeeDefaults()
const TODAY = '2026-09-30'

const op = (overrides: Partial<QueuedOp>): QueuedOp => ({
  seq: 1,
  userId: 'u1',
  itemId: 'a',
  kind: 'update',
  payload: null,
  baseVersion: '"v1"',
  label: 'Tee',
  createdAt: '2026-09-30T10:00:00Z',
  state: 'pending',
  ...overrides,
})

const fields = (overrides: Partial<ItemRequestDto> = {}): ItemRequestDto => ({
  title: 'Tee', brand: '', category: '', size: '', condition: 'Good', notes: '', cost: 8, source: '',
  acquiredDate: '2026-09-01', platforms: [], listPrice: null, listedDate: null, ...overrides,
})

describe('applyOp', () => {
  it('creates an item offline with the same status and projected net the server would give it', () => {
    const created = applyOp(undefined, op({ kind: 'create', itemId: 'new', payload: fields({ platforms: ['ebay'], listPrice: 40 }) }), fees, TODAY)!

    expect(created.id).toBe('new')
    expect(created.status).toBe('listed')
    expect(created.listedDate).toBe(TODAY)
    expect(created.projectedNet).toBe(26.3) // 40 - (40 x 13.25% + 0.40) - 8
  })

  it('puts a listed item back in stock when its last platform is removed', () => {
    const listed = itemDto({ id: 'a', status: 'listed', platforms: ['depop'], listedDate: '2026-09-01' })

    const updated = applyOp(listed, op({ payload: fields({ platforms: [], listedDate: '2026-09-01' }) }), fees, TODAY)!

    expect(updated.status).toBe('inventory')
  })

  it('works out a sale made offline with the shared profit math, fees estimated', () => {
    const listed = itemDto({ id: 'a', status: 'listed', cost: 8, platforms: ['ebay'] })

    const sold = applyOp(
      listed,
      op({ kind: 'sell', payload: { platform: 'ebay', listedFor: 40, price: 40, payout: null, shippingCharged: 5, shippingCost: 4.5, otherCosts: 0.5, date: TODAY } }),
      fees,
    )!

    expect(sold.status).toBe('sold')
    expect(sold.profit).toMatchObject({ fees: 6.36, net: 25.64, feesEstimated: true })
    expect(sold.projectedNet).toBeNull()
  })

  it('sends an undone sale back to listed while it is still on a platform', () => {
    const sold = itemDto({ id: 'a', status: 'sold', platforms: ['depop'], sale: { platform: 'depop', listedFor: 0, price: 10, payout: 10, shippingCharged: 0, shippingCost: 0, otherCosts: 0, date: TODAY } })

    const undone = applyOp(sold, op({ kind: 'undoSale' }), fees)!

    expect(undone.status).toBe('listed')
    expect(undone.sale).toBeNull()
    expect(undone.profit).toBeNull()
  })

  it('removes a deleted item, and ignores a change to one that is gone', () => {
    expect(applyOp(itemDto({ id: 'a' }), op({ kind: 'delete' }), fees)).toBeNull()
    expect(applyOp(undefined, op({ kind: 'update', payload: fields() }), fees)).toBeNull()
  })
})

describe('applyPending', () => {
  it('lays pending changes over the server items and marks them unsynced', () => {
    const server = [itemDto({ id: 'a', title: 'Old title', createdAt: '2026-09-01T00:00:00Z' })]
    const { items, unsynced } = applyPending(
      server,
      [
        op({ seq: 1, kind: 'update', itemId: 'a', payload: fields({ title: 'New title' }) }),
        op({ seq: 2, kind: 'create', itemId: 'b', payload: fields({ title: 'Made offline' }), createdAt: '2026-09-30T00:00:00Z' }),
      ],
      fees,
    )

    expect(items.map((i) => i.title)).toEqual(['Made offline', 'New title'])
    expect([...unsynced]).toEqual(['a', 'b'])
  })

  it('shows the server version while a change waits on a decision', () => {
    const server = [itemDto({ id: 'a', title: 'Theirs' })]

    const { items, unsynced } = applyPending(server, [op({ state: 'conflict', payload: fields({ title: 'Mine' }) })], fees)

    expect(items[0]!.title).toBe('Theirs')
    expect(unsynced.size).toBe(0)
  })
})
