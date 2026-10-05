import { describe, it, expect } from 'vitest'
import { lastMonthKeys, rangeKeys, monthlyTotals, cumulativeNet, relativeChange } from './series'
import { defaultFeeSettings } from './platforms'

const fees = defaultFeeSettings()
const now = new Date(2026, 9, 15) // 15 Oct 2026

// Payouts entered, so fees are exact: net = payout - cost.
const sale = (date, payout, cost = 0) => ({
  status: 'sold',
  cost,
  sale: { platform: 'vinted', price: payout, payout, date },
})

describe('lastMonthKeys', () => {
  it('ends on the current month, oldest first', () => {
    expect(lastMonthKeys(3, now)).toEqual(['2026-08', '2026-09', '2026-10'])
  })

  it('crosses a year boundary', () => {
    expect(lastMonthKeys(3, new Date(2026, 0, 10))).toEqual(['2025-11', '2025-12', '2026-01'])
  })
})

describe('rangeKeys', () => {
  it('drops months before the first sale', () => {
    expect(rangeKeys([sale('2026-09-02', 10)], 6, now)).toEqual(['2026-09', '2026-10'])
  })

  it('keeps the full range when there are no sales', () => {
    expect(rangeKeys([], 3, now)).toHaveLength(3)
  })

  it('keeps the full range when the first sale is older than it', () => {
    expect(rangeKeys([sale('2025-01-01', 10)], 3, now)).toEqual(['2026-08', '2026-09', '2026-10'])
  })
})

describe('monthlyTotals', () => {
  it('buckets sales by month and leaves quiet months at zero', () => {
    const sold = [sale('2026-08-03', 50, 10), sale('2026-08-20', 20), sale('2026-10-01', 15, 5)]
    const rows = monthlyTotals(sold, fees, ['2026-08', '2026-09', '2026-10'])
    expect(rows.map(r => r.net)).toEqual([60, 0, 10])
    expect(rows.map(r => r.count)).toEqual([2, 0, 1])
  })
})

describe('cumulativeNet', () => {
  it('carries in profit from before the range', () => {
    const sold = [sale('2026-01-05', 100), sale('2026-09-10', 30), sale('2026-10-02', 0, 5)]
    const rows = cumulativeNet(sold, fees, ['2026-08', '2026-09', '2026-10'])
    expect(rows.map(r => r.total)).toEqual([100, 130, 125])
  })
})

describe('relativeChange', () => {
  it('is a signed fraction of the previous figure', () => {
    expect(relativeChange(100, 125)).toBe(0.25)
    expect(relativeChange(100, 50)).toBe(-0.5)
  })

  it('measures against the size of a negative base', () => {
    expect(relativeChange(-100, -50)).toBe(0.5)
  })

  it('has no answer from a zero base', () => {
    expect(relativeChange(0, 40)).toBeNull()
  })
})
