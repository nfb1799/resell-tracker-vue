// Mirror of the server's lifecycle rules (ResellTracker.Domain/Lifecycle.cs), so
// the UI only offers what the server will accept.
//
//   inventory → listed → sold      the money came back
//                      → donated   it did not, and the cost is written off
//
// Inventory and listed follow the item's platforms rather than being picked.
// Either one is stock on hand: what "cash tied up" and the aging buckets count.

export type ItemStatus = 'inventory' | 'listed' | 'sold' | 'donated'

export const STATUS_LABELS: Record<ItemStatus, string> = {
  inventory: 'In stock',
  listed: 'Listed',
  sold: 'Sold',
  donated: 'Donated',
}

export const isOnHand = (status: ItemStatus): boolean => status === 'inventory' || status === 'listed'

/**
 * Adding the first platform lists a stocked item (listed date defaulting to
 * today); removing the last puts it back in stock. Sold and donated items keep
 * their status whatever their platforms say.
 */
export function applyPlatforms(
  status: ItemStatus,
  platforms: readonly string[],
  listedDate: string | null,
  today: string,
): { status: ItemStatus; listedDate: string | null } {
  const onPlatform = platforms.length > 0
  if (status === 'inventory' && onPlatform) return { status: 'listed', listedDate: listedDate || today }
  if (status === 'listed' && !onPlatform) return { status: 'inventory', listedDate }
  return { status, listedDate }
}

/** Anything on hand can be sold, and a sold item's sale can be edited. */
export const canSell = (status: ItemStatus): boolean => status !== 'donated'

/** Anything on hand can be donated, and a donated item's donation can be edited. */
export const canDonate = (status: ItemStatus): boolean => status !== 'sold'

/** Where an undone sale or donation lands: listed if still on a platform, else stock. */
export const statusAfterUndo = (platforms: readonly string[]): ItemStatus =>
  platforms.length > 0 ? 'listed' : 'inventory'
