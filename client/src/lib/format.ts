// Display formatting for dates. Money formatting lives in domain/money.ts.

import { daysListed as daysListedFor, getLocalDateString, monthKey } from '@/domain/dates'
import type { Item } from '@/domain/item'

function parse(date: string | null | undefined): Date | null {
  if (!date) return null
  const [y, m, d] = date.split('-').map(Number)
  return y && m && d ? new Date(y, m - 1, d) : null
}

/** "Sep 4", or "Sep 4, 2025" outside the current year. */
export function formatDate(date: string | null | undefined): string {
  const d = parse(date)
  if (!d) return '—'
  const sameYear = d.getFullYear() === new Date().getFullYear()
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', ...(sameYear ? {} : { year: 'numeric' }) })
}

/** "Sep 2026" for a 'YYYY-MM' key. */
export function monthLabel(key: string): string {
  const d = parse(`${key}-01`)
  return d ? d.toLocaleDateString(undefined, { month: 'short', year: 'numeric' }) : '—'
}

/** This month where the user is, e.g. "Sep 2026". */
export const currentMonthLabel = (): string => monthLabel(monthKey(getLocalDateString()))

/** Short axis label: "Sep", with the year on January so the span stays readable. */
export function monthTick(key: string): string {
  const d = parse(`${key}-01`)
  if (!d) return ''
  return d.toLocaleDateString(undefined, { month: 'short', ...(d.getMonth() === 0 ? { year: '2-digit' } : {}) })
}

/** Days an item has sat, counted to today where the user is. */
export const itemDaysListed = (item: Item): number | null =>
  daysListedFor({
    status: item.status,
    acquiredDate: item.acquiredDate,
    listedDate: item.listedDate,
    saleDate: item.sale?.date ?? null,
  })
