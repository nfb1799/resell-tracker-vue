// Runs every case in shared/profit-cases.json against the TypeScript mirror. The
// server's xUnit suite runs the same file against the C# domain.

import { describe, it, expect } from 'vitest'
import fixture from '@shared/profit-cases.json'
import { fromDollars, type Cents } from '../money'
import { withFeeDefaults, type FeeSchedule } from '../platforms'
import { computeProfit, estimateFees, projectedNet, totalProfit, writeOffTotal, type SaleFigures } from '../profit'
import { daysListed } from '../dates'
import type { ItemStatus } from '../lifecycle'

interface FixtureSale {
  platform: string
  price: number
  shippingCharged: number
  payout: number | null
  shippingCost: number
  otherCosts: number
}
interface FixtureItem {
  cost: number
  sale: FixtureSale | null
}

const feesFor = (c: { feeOverrides?: Record<string, FeeSchedule> }) => withFeeDefaults(c.feeOverrides)

const saleCents = (s: FixtureSale | null): SaleFigures | null =>
  s && {
    platform: s.platform,
    price: fromDollars(s.price),
    shippingCharged: fromDollars(s.shippingCharged),
    payout: s.payout === null ? null : fromDollars(s.payout),
    shippingCost: fromDollars(s.shippingCost),
    otherCosts: fromDollars(s.otherCosts),
  }

const compute = (item: FixtureItem, c: { feeOverrides?: Record<string, FeeSchedule> }) =>
  computeProfit(fromDollars(item.cost), saleCents(item.sale), feesFor(c))

const nullableCents = (n: number | null): Cents | null => (n === null ? null : fromDollars(n))

// Ratios are either both missing or equal to within 1e-9.
function expectRatio(actual: number | null, expected: number | null) {
  expect(actual === null).toBe(expected === null)
  const gap = actual === null || expected === null ? 0 : Math.abs(actual - expected)
  expect(gap).toBeLessThan(1e-9)
}

describe('estimateFees', () => {
  it.each(fixture.estimateFees.map((c) => [c.name, c] as const))('%s', (_, c) => {
    const fees = estimateFees(
      { platform: c.sale.platform, price: fromDollars(c.sale.price), shippingCharged: fromDollars(c.sale.shippingCharged) },
      feesFor(c),
    )
    expect(fees).toBe(fromDollars(c.expected))
  })
})

describe('computeProfit', () => {
  it.each(fixture.computeProfit.map((c) => [c.name, c] as const))('%s', (_, c) => {
    const p = compute(c.item as FixtureItem, c)
    const e = c.expected
    expect(p.gross).toBe(fromDollars(e.gross))
    expect(p.payout).toBe(fromDollars(e.payout))
    expect(p.fees).toBe(fromDollars(e.fees))
    expect(p.cogs).toBe(fromDollars(e.cogs))
    expect(p.shippingCost).toBe(fromDollars(e.shippingCost))
    expect(p.otherCosts).toBe(fromDollars(e.otherCosts))
    expect(p.costs).toBe(fromDollars(e.costs))
    expect(p.net).toBe(fromDollars(e.net))
    expectRatio(p.margin, e.margin)
    expectRatio(p.roi, e.roi)
    expect(p.feesEstimated).toBe(e.feesEstimated)
  })
})

describe('totalProfit', () => {
  it.each(fixture.totalProfit.map((c) => [c.name, c] as const))('%s', (_, c) => {
    const t = totalProfit((c.items as FixtureItem[]).map((item) => compute(item, {})))
    expect(t).toEqual({
      gross: fromDollars(c.expected.gross),
      payout: fromDollars(c.expected.payout),
      fees: fromDollars(c.expected.fees),
      costs: fromDollars(c.expected.costs),
      net: fromDollars(c.expected.net),
      count: c.expected.count,
      estimated: c.expected.estimated,
    })
  })
})

describe('writeOffTotal', () => {
  it.each(fixture.writeOffTotal.map((c) => [c.name, c] as const))('%s', (_, c) => {
    const w = writeOffTotal(c.items.map((i) => fromDollars(i.cost)))
    expect(w).toEqual({ cost: fromDollars(c.expected.cost), count: c.expected.count })
  })
})

describe('projectedNet', () => {
  it.each(fixture.projectedNet.map((c) => [c.name, c] as const))('%s', (_, c) => {
    const net = projectedNet(
      { listPrice: nullableCents(c.item.listPrice), cost: fromDollars(c.item.cost), platforms: c.item.platforms },
      feesFor({}),
    )
    expect(net).toBe(nullableCents(c.expected))
  })
})

describe('daysListed', () => {
  it.each(fixture.daysListed.map((c) => [c.name, c] as const))('%s', (_, c) => {
    const item = { ...c.item, status: c.item.status as ItemStatus }
    expect(daysListed(item, c.today)).toBe(c.expected)
  })
})
