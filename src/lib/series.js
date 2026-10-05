// Month-by-month series for the charts. Pure functions only — see series.test.js.
//
// Months are 'YYYY-MM' keys, oldest first, and every calendar month in a range
// is present so a quiet month reads as a gap rather than being skipped.

import { totalProfit } from './money'
import { monthKey } from './date'

const keyOf = (date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`

// The `count` calendar months ending with the one `now` falls in.
export function lastMonthKeys(count, now = new Date()) {
  const keys = []
  for (let back = count - 1; back >= 0; back--) {
    keys.push(keyOf(new Date(now.getFullYear(), now.getMonth() - back, 1)))
  }
  return keys
}

// The last `count` months, trimmed so the range never starts before the first
// sale: four months of history shows four bars, not eight empty ones first.
export function rangeKeys(sold, count, now = new Date()) {
  const keys = lastMonthKeys(count, now)
  const first = sold.map(i => monthKey(i.sale?.date)).filter(Boolean).sort()[0]
  if (!first) return keys
  const trimmed = keys.filter(k => k >= first)
  return trimmed.length ? trimmed : keys.slice(-1)
}

// Profit totals for each month in `keys`.
export function monthlyTotals(sold, feeSettings, keys) {
  return keys.map(key => {
    const t = totalProfit(sold.filter(i => monthKey(i.sale?.date) === key), feeSettings)
    return { key, net: t.net, gross: t.gross, fees: t.fees, count: t.count }
  })
}

// All-time net profit as it stood at the end of each month in `keys` — sales
// from before the range still count, so the line starts where the money was.
export function cumulativeNet(sold, feeSettings, keys) {
  return keys.map(key => ({
    key,
    total: totalProfit(sold.filter(i => {
      const k = monthKey(i.sale?.date)
      return k && k <= key
    }), feeSettings).net,
  }))
}

// Short axis tick: "Mar", with the year on January so a span stays readable.
export function tickLabel(key) {
  const [y, m] = key.split('-').map(Number)
  const d = new Date(y, m - 1, 1)
  return d.toLocaleDateString(undefined, { month: 'short', ...(m === 1 ? { year: '2-digit' } : {}) })
}

// Change from one figure to the next as a fraction, or null when there is no
// meaningful base to compare against.
export function relativeChange(previous, current) {
  if (!Number.isFinite(previous) || !Number.isFinite(current) || previous === 0) return null
  return (current - previous) / Math.abs(previous)
}
