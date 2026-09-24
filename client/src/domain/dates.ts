// Dates are plain 'YYYY-MM-DD' calendar strings, as the API sends them, so they
// never shift across time zones the way Date objects do.

import type { ItemStatus } from './lifecycle'

const DATE_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/

export function getLocalDateString(date = new Date()): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

/** Days since an arbitrary epoch, for a 'YYYY-MM-DD' string; null if malformed. */
function dayNumber(date: string | null | undefined): number | null {
  const match = date ? DATE_PATTERN.exec(date) : null
  if (!match) return null
  const [, y, m, d] = match
  return Date.UTC(Number(y), Number(m) - 1, Number(d)) / 86_400_000
}

/** Whole days from one date to another (default today), never negative. */
export function daysBetween(from: string | null | undefined, to?: string | null): number | null {
  const start = dayNumber(from)
  if (start === null) return null
  const end = dayNumber(to) ?? dayNumber(getLocalDateString())!
  return Math.max(0, end - start)
}

/** How long an item has been sitting: listed (or acquired) to sold, or to today. */
export function daysListed(
  item: { status: ItemStatus; acquiredDate: string | null; listedDate: string | null; saleDate: string | null },
  today = getLocalDateString(),
): number | null {
  const start = item.listedDate || item.acquiredDate
  const end = item.status === 'sold' && item.saleDate ? item.saleDate : today
  return daysBetween(start, end)
}

export const monthKey = (date: string | null | undefined): string =>
  date && date.length >= 7 ? date.slice(0, 7) : ''
