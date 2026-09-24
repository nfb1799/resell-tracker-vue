import { describe, it, expect } from 'vitest'
import { itemDto } from '@/__tests__/fixtures'
import { fromDto, toRequest } from '../item'
import { cents } from '../money'

describe('item model', () => {
  it('turns every amount from the API into whole cents', () => {
    const item = fromDto(
      itemDto({
        cost: 0.29,
        listPrice: 1204.5,
        projectedNet: -3.1,
        sale: { platform: 'ebay', listedFor: 40, price: 38.64, payout: null, shippingCharged: 5, shippingCost: 4.5, otherCosts: 0.5, date: '2026-09-20' },
      }),
    )

    expect(item.cost).toBe(29)
    expect(item.listPrice).toBe(120450)
    expect(item.projectedNet).toBe(-310)
    expect(item.sale?.price).toBe(3864)
    expect(item.sale?.payout).toBeNull()
  })

  it('sends cents back to the API as dollars, exactly', () => {
    const request = toRequest(
      {
        title: 'Tee',
        brand: '',
        category: '',
        size: '',
        condition: 'Good',
        notes: '',
        cost: cents(29),
        source: '',
        acquiredDate: null,
        platforms: ['depop'],
        listPrice: null,
        listedDate: null,
      },
      'abc',
    )

    expect(request).toMatchObject({ id: 'abc', cost: 0.29, listPrice: null, platforms: ['depop'] })
  })
})
