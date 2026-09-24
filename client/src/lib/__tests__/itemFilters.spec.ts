import { describe, it, expect } from 'vitest'
import { item } from '@/__tests__/fixtures'
import { filterItems, statusCounts } from '../itemFilters'

const base = { status: 'all', platform: 'all', search: '', sort: 'newest' } as const

describe('filterItems', () => {
  const crossListedThenSoldOnEbay = item({
    title: 'Jacket',
    status: 'sold',
    platforms: ['depop', 'ebay'],
    sale: { platform: 'ebay', listedFor: 40, price: 35, payout: 30, shippingCharged: 0, shippingCost: 0, otherCosts: 0, date: '2026-09-10' },
  })
  const onDepop = item({ title: 'Tee', status: 'listed', platforms: ['depop'], createdAt: '2026-09-05T00:00:00Z' })
  const donated = item({
    title: 'Scarf',
    status: 'donated',
    platforms: ['vinted'],
    donation: { date: '2026-09-12', org: 'Goodwill', receiptValue: null },
    createdAt: '2026-08-01T00:00:00Z',
  })
  const all = [crossListedThenSoldOnEbay, onDepop, donated]

  it('matches a sold item by the platform it sold on, not everywhere it was listed', () => {
    expect(filterItems(all, { ...base, platform: 'depop' }).map((i) => i.title)).toEqual(['Tee'])
    expect(filterItems(all, { ...base, platform: 'ebay' }).map((i) => i.title)).toEqual(['Jacket'])
  })

  it('keeps a donated item under the platform it was listed on', () => {
    expect(filterItems(all, { ...base, platform: 'vinted' }).map((i) => i.title)).toEqual(['Scarf'])
  })

  it('searches the donation org and ignores case', () => {
    expect(filterItems(all, { ...base, search: '  GOODWILL ' }).map((i) => i.title)).toEqual(['Scarf'])
  })

  it('filters by status', () => {
    expect(filterItems(all, { ...base, status: 'listed' }).map((i) => i.title)).toEqual(['Tee'])
  })

  it('sorts by asking price, treating none as zero', () => {
    const cheap = item({ title: 'Cheap', listPrice: 5 })
    const dear = item({ title: 'Dear', listPrice: 50 })
    const unpriced = item({ title: 'Unpriced' })
    expect(filterItems([cheap, unpriced, dear], { ...base, sort: 'priceHigh' }).map((i) => i.title)).toEqual(['Dear', 'Cheap', 'Unpriced'])
  })

  it('does not reorder the list it was given', () => {
    const list = [donated, onDepop]
    filterItems(list, { ...base, sort: 'title' })
    expect(list).toEqual([donated, onDepop])
  })
})

describe('statusCounts', () => {
  it('counts every status and the total', () => {
    const counts = statusCounts([item({ status: 'sold' }), item({ status: 'sold' }), item({ status: 'listed' })])
    expect(counts).toEqual({ all: 3, inventory: 0, listed: 1, sold: 2, donated: 0 })
  })
})
