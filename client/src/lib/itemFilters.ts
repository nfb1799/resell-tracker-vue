// Inventory search, filters and sorts: pure, so the page stays declarative and
// the rules are testable. Matches the original app, and the API's own filters.

import type { Item } from '@/domain/item'
import type { ItemStatus } from '@/domain/lifecycle'
import { itemDaysListed } from './format'

export type StatusFilter = 'all' | ItemStatus

export const STATUS_FILTERS: readonly { id: StatusFilter; label: string }[] = [
  { id: 'all', label: 'All' },
  { id: 'listed', label: 'Listed' },
  { id: 'inventory', label: 'In stock' },
  { id: 'sold', label: 'Sold' },
  { id: 'donated', label: 'Donated' },
]

export type SortId = 'newest' | 'oldest' | 'priceHigh' | 'priceLow' | 'title'

export const SORTS: Record<SortId, { label: string; compare: (a: Item, b: Item) => number }> = {
  newest: { label: 'Newest first', compare: (a, b) => b.createdAt.localeCompare(a.createdAt) },
  oldest: { label: 'Longest listed', compare: (a, b) => (itemDaysListed(b) ?? -1) - (itemDaysListed(a) ?? -1) },
  priceHigh: { label: 'Price: high to low', compare: (a, b) => (b.listPrice ?? 0) - (a.listPrice ?? 0) },
  priceLow: { label: 'Price: low to high', compare: (a, b) => (a.listPrice ?? 0) - (b.listPrice ?? 0) },
  title: { label: 'Title A–Z', compare: (a, b) => a.title.localeCompare(b.title) },
}

export function matchesSearch(item: Item, needle: string): boolean {
  if (!needle) return true
  const haystack = [item.title, item.brand, item.category, item.size, item.source, item.notes, item.donation?.org]
    .filter(Boolean)
    .join(' ')
    .toLowerCase()
  return haystack.includes(needle)
}

/**
 * A sold item belongs to the platform it actually sold on; anything else (a
 * donated one included) to whatever it is or was listed on.
 */
export function onPlatform(item: Item, platform: string): boolean {
  return item.status === 'sold' ? item.sale?.platform === platform : item.platforms.includes(platform)
}

export function filterItems(
  items: readonly Item[],
  { status, platform, search, sort }: { status: StatusFilter; platform: string; search: string; sort: SortId },
): Item[] {
  const needle = search.trim().toLowerCase()
  return items
    .filter((item) => status === 'all' || item.status === status)
    .filter((item) => platform === 'all' || onPlatform(item, platform))
    .filter((item) => matchesSearch(item, needle))
    .sort(SORTS[sort].compare)
}

export function statusCounts(items: readonly Item[]): Record<StatusFilter, number> {
  const counts: Record<StatusFilter, number> = { all: items.length, inventory: 0, listed: 0, sold: 0, donated: 0 }
  for (const item of items) counts[item.status]++
  return counts
}
