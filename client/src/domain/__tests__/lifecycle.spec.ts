import { describe, it, expect } from 'vitest'
import { applyPlatforms, canDonate, canSell, isOnHand, statusAfterUndo } from '../lifecycle'
import { PLATFORM_IDS, platformLabel, scheduleFor, withFeeDefaults } from '../platforms'

describe('lifecycle', () => {
  it('counts only inventory and listed as stock on hand', () => {
    expect(isOnHand('inventory')).toBe(true)
    expect(isOnHand('listed')).toBe(true)
    expect(isOnHand('sold')).toBe(false)
    expect(isOnHand('donated')).toBe(false)
  })

  it('lists a stocked item when it gets its first platform, dated today unless already dated', () => {
    expect(applyPlatforms('inventory', ['depop'], null, '2026-09-24')).toEqual({ status: 'listed', listedDate: '2026-09-24' })
    expect(applyPlatforms('inventory', ['depop'], '2026-09-01', '2026-09-24').listedDate).toBe('2026-09-01')
  })

  it('puts a listed item back in stock when its last platform is removed', () => {
    expect(applyPlatforms('listed', [], '2026-09-01', '2026-09-24')).toEqual({ status: 'inventory', listedDate: '2026-09-01' })
  })

  it('never lets platforms change a sold or donated item', () => {
    expect(applyPlatforms('sold', [], null, '2026-09-24').status).toBe('sold')
    expect(applyPlatforms('donated', ['ebay'], null, '2026-09-24').status).toBe('donated')
  })

  it('sells anything on hand, including never-listed stock, and edits a sale', () => {
    expect(canSell('inventory')).toBe(true)
    expect(canSell('listed')).toBe(true)
    expect(canSell('sold')).toBe(true)
    expect(canSell('donated')).toBe(false)
  })

  it('donates anything on hand and edits a donation, but not a sold item', () => {
    expect(canDonate('inventory')).toBe(true)
    expect(canDonate('listed')).toBe(true)
    expect(canDonate('donated')).toBe(true)
    expect(canDonate('sold')).toBe(false)
  })

  it('sends an undone item back to listed only while it is still on a platform', () => {
    expect(statusAfterUndo(['depop'])).toBe('listed')
    expect(statusAfterUndo([])).toBe('inventory')
  })
})

describe('platforms', () => {
  it('reads the shared registry in display order', () => {
    expect(PLATFORM_IDS).toEqual(['depop', 'ebay', 'vinted', 'other'])
    expect(platformLabel('ebay')).toBe('eBay')
    expect(platformLabel('grailed')).toBe('grailed')
    expect(platformLabel(null)).toBe('—')
  })

  it('fills platforms missing from saved settings with their defaults', () => {
    const fees = withFeeDefaults({ ebay: { percent: 10 } })
    expect(fees.ebay).toEqual({ percent: 10, fixed: 0.4, includesShipping: true })
    expect(fees.depop).toEqual({ percent: 3.3, fixed: 0.45, includesShipping: true })
  })

  it('falls back to the "other" schedule for an unknown platform', () => {
    expect(scheduleFor(withFeeDefaults(), 'grailed')).toEqual({ percent: 0, fixed: 0, includesShipping: false })
  })
})
