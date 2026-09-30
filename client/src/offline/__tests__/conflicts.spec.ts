import { describe, it, expect } from 'vitest'
import { itemDto } from '@/__tests__/fixtures'
import type { QueuedOp } from '../db'
import { conflictRows, mergeEdit } from '../conflicts'

const op = (overrides: Partial<QueuedOp>): QueuedOp => ({
  seq: 1, userId: 'u1', itemId: 'a', kind: 'update', payload: null, baseVersion: '"v1"',
  label: 'Tee', createdAt: '2026-09-30T10:00:00Z', state: 'conflict', ...overrides,
})

const baseFields = () => ({ title: 'Tee', brand: '', category: '', size: '', condition: 'Good', notes: '', cost: 8, source: '', acquiredDate: '2026-09-01', platforms: ['depop'], listPrice: 20, listedDate: '2026-09-01' })

describe('conflictRows', () => {
  it('lists only the fields that differ, yours next to theirs', () => {
    const theirs = itemDto({ ...baseFields(), status: 'listed', listPrice: 25 })

    const rows = conflictRows(op({ payload: { ...baseFields(), title: 'Tee, red' } }), theirs, 'USD')

    expect(rows).toEqual([
      { field: 'Title', yours: 'Tee, red', theirs: 'Tee' },
      { field: 'Asking price', yours: '$20.00', theirs: '$25.00' },
    ])
  })

  it('lists only what this edit changed, not fields it carried along', () => {
    const base = itemDto({ ...baseFields(), status: 'listed' })
    // Offline I renamed it; meanwhile someone else lowered the price.
    const theirs = itemDto({ ...baseFields(), status: 'listed', listPrice: 15 })

    const rows = conflictRows(op({ payload: { ...baseFields(), title: 'Tee, red' }, base }), theirs, 'USD')

    expect(rows).toEqual([{ field: 'Title', yours: 'Tee, red', theirs: 'Tee' }])
  })

  it('keeping mine applies only what I changed, on top of their edit', () => {
    const base = itemDto({ ...baseFields() })
    const theirs = itemDto({ ...baseFields(), listPrice: 15, notes: 'Small mark on the cuff' })

    const merged = mergeEdit(op({ payload: { ...baseFields(), title: 'Tee, red' }, base }), theirs)

    expect(merged).toMatchObject({ title: 'Tee, red', listPrice: 15, notes: 'Small mark on the cuff' })
  })

  it('finds nothing when the server already has this exact change', () => {
    const theirs = itemDto({ ...baseFields(), status: 'listed' })

    expect(conflictRows(op({ payload: { ...baseFields() } }), theirs, 'USD')).toEqual([])
  })

  it('compares a sale field by field, status included', () => {
    const sale = { platform: 'ebay', listedFor: 40, price: 35, payout: null, shippingCharged: 0, shippingCost: 0, otherCosts: 0, date: '2026-09-29' }
    const theirs = itemDto({ ...baseFields(), status: 'donated' })

    const rows = conflictRows(op({ kind: 'sell', payload: sale }), theirs, 'USD')

    expect(rows[0]).toEqual({ field: 'Status', yours: 'Sold', theirs: 'Donated' })
    expect(rows.map((r) => r.field)).toContain('Offer accepted')
  })

  it('treats an undo the server has already done as no conflict', () => {
    expect(conflictRows(op({ kind: 'undoSale' }), itemDto({ status: 'listed' }), 'USD')).toEqual([])
    expect(conflictRows(op({ kind: 'undoSale' }), itemDto({ status: 'sold' }), 'USD')).toHaveLength(1)
  })

  it('always asks before deleting something changed elsewhere', () => {
    expect(conflictRows(op({ kind: 'delete' }), itemDto({}), 'USD')).toHaveLength(1)
  })
})
