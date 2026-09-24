// Mirror of the server's profit math (ResellTracker.Domain/Profit.cs), used for
// the sale form's live breakdown. The server stays the source of truth; both
// implementations run shared/profit-cases.json so they cannot drift.

import { add, cents, divideRoundHalfUp, fromDollars, sub, ZERO, type Cents } from './money'
import { FALLBACK_PLATFORM, scheduleFor, type FeeSettings } from './platforms'

/** What a sale recorded. `payout` null means "not known yet". */
export interface SaleFigures {
  platform: string
  price: Cents
  shippingCharged: Cents
  payout: Cents | null
  shippingCost: Cents
  otherCosts: Cents
}

export interface ProfitBreakdown {
  gross: Cents
  payout: Cents
  fees: Cents
  cogs: Cents
  shippingCost: Cents
  otherCosts: Cents
  costs: Cents
  net: Cents
  /** Net over gross; null with no revenue. */
  margin: number | null
  /** Net over cost of goods; null for an item that cost nothing. */
  roi: number | null
  feesEstimated: boolean
}

export interface ProfitTotals {
  gross: Cents
  payout: Cents
  fees: Cents
  costs: Cents
  net: Cents
  count: number
  estimated: number
}

/**
 * What the platform's rate table says a sale should cost. Only ever a stand-in
 * for the real figure: see computeProfit.
 */
export function estimateFees(
  sale: { platform: string | null; price: Cents; shippingCharged: Cents },
  fees: FeeSettings,
): Cents {
  const schedule = scheduleFor(fees, sale.platform)
  const base = schedule.includesShipping ? sale.price + sale.shippingCharged : sale.price
  if (base <= 0) return ZERO

  // Percent has at most 2 decimals, so it is a whole number of hundredths of a
  // percent, and base * that / 10000 is the fee in cents before rounding.
  const hundredthsOfPercent = Math.round(schedule.percent * 100)
  const percentPart = divideRoundHalfUp(base * hundredthsOfPercent, 10000)
  return add(cents(percentPart), fromDollars(schedule.fixed))
}

/**
 * gross = what the buyer paid; net = payout less cost of goods, shipping and other
 * costs. The payout is the pivot: entered, the fee is derived from it and is exact;
 * left null, the rate table estimates the fee and the payout follows.
 */
export function computeProfit(cost: Cents, sale: SaleFigures | null, fees: FeeSettings): ProfitBreakdown {
  const gross = add(sale?.price ?? ZERO, sale?.shippingCharged ?? ZERO)

  const payoutKnown = sale?.payout !== null && sale?.payout !== undefined
  let payout: Cents
  let platformFees: Cents
  if (payoutKnown) {
    payout = sale.payout!
    platformFees = sub(gross, payout)
  } else {
    platformFees = estimateFees(
      { platform: sale?.platform ?? null, price: sale?.price ?? ZERO, shippingCharged: sale?.shippingCharged ?? ZERO },
      fees,
    )
    payout = sub(gross, platformFees)
  }

  const shippingCost = sale?.shippingCost ?? ZERO
  const otherCosts = sale?.otherCosts ?? ZERO
  const costs = add(cost, shippingCost, otherCosts)
  const net = sub(payout, costs)

  return {
    gross,
    payout,
    fees: platformFees,
    cogs: cost,
    shippingCost,
    otherCosts,
    costs,
    net,
    margin: gross > 0 ? net / gross : null,
    roi: cost > 0 ? net / cost : null,
    feesEstimated: !payoutKnown,
  }
}

export function totalProfit(sales: readonly ProfitBreakdown[]): ProfitTotals {
  return sales.reduce<ProfitTotals>(
    (t, p) => ({
      gross: add(t.gross, p.gross),
      payout: add(t.payout, p.payout),
      fees: add(t.fees, p.fees),
      costs: add(t.costs, p.costs),
      net: add(t.net, p.net),
      count: t.count + 1,
      estimated: t.estimated + (p.feesEstimated ? 1 : 0),
    }),
    { gross: ZERO, payout: ZERO, fees: ZERO, costs: ZERO, net: ZERO, count: 0, estimated: 0 },
  )
}

/**
 * Donated stock never earns its cost back, so that cost is a write-off, kept
 * apart from sale profit rather than folded into it.
 */
export function writeOffTotal(donatedCosts: readonly Cents[]): { cost: Cents; count: number } {
  return { cost: add(...donatedCosts), count: donatedCosts.length }
}

/**
 * What a listing would net at its asking price on the first platform it is listed
 * on. Shipping is unknown before it sells, so this is price only.
 */
export function projectedNet(
  item: { listPrice: Cents | null; cost: Cents; platforms: readonly string[] },
  fees: FeeSettings,
): Cents | null {
  const price = item.listPrice
  if (price === null || price <= 0) return null
  const platform = item.platforms[0] ?? FALLBACK_PLATFORM
  const platformFees = estimateFees({ platform, price, shippingCharged: ZERO }, fees)
  return sub(sub(price, platformFees), item.cost)
}

/** How far under the asking price the accepted offer landed, or null if it didn't. */
export function markdown(listedFor: Cents, accepted: Cents): { amount: Cents; fraction: number } | null {
  if (listedFor <= 0 || accepted <= 0 || accepted >= listedFor) return null
  return { amount: sub(listedFor, accepted), fraction: (listedFor - accepted) / listedFor }
}
