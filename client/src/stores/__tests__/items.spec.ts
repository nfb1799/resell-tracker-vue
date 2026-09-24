import { beforeEach, describe, it, expect, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { itemDto } from '@/__tests__/fixtures'
import { ApiError, itemsApi } from '@/api'
import { cents } from '@/domain/money'
import { useItemsStore } from '../items'
import { useStatsStore } from '../stats'
import { useToastStore } from '../toast'

vi.mock('@/api', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api')>()
  return {
    ...actual,
    itemsApi: {
      list: vi.fn<typeof actual.itemsApi.list>(),
      create: vi.fn<typeof actual.itemsApi.create>(),
      update: vi.fn<typeof actual.itemsApi.update>(),
      sell: vi.fn<typeof actual.itemsApi.sell>(),
      remove: vi.fn<typeof actual.itemsApi.remove>(),
    },
  }
})

const api = vi.mocked(itemsApi)

describe('items store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.resetAllMocks()
  })

  it('puts a newly created item first and marks the stats stale', async () => {
    const store = useItemsStore()
    api.list.mockResolvedValue([itemDto({ title: 'Older' })])
    await store.load()
    const revision = useStatsStore().revision
    api.create.mockResolvedValue(itemDto({ title: 'Newer' }))

    await store.create({
      title: 'Newer', brand: '', category: '', size: '', condition: 'Good', notes: '',
      cost: cents(800), source: '', acquiredDate: null, platforms: [], listPrice: null, listedDate: null,
    })

    expect(store.items.map((i) => i.title)).toEqual(['Newer', 'Older'])
    expect(useStatsStore().revision).toBeGreaterThan(revision)
    // A client-made id, so a replayed offline create is recognisable, and dollars on the wire.
    expect(api.create.mock.calls[0]![0]).toMatchObject({ cost: 8 })
    expect(api.create.mock.calls[0]![0].id).toMatch(/^[0-9a-f-]{36}$/)
  })

  it('sends a sale in dollars with the version it last saw', async () => {
    const store = useItemsStore()
    api.list.mockResolvedValue([itemDto({ id: 'a', version: '"v1"' })])
    await store.load()
    api.sell.mockResolvedValue(itemDto({ id: 'a', status: 'sold' }))

    await store.sell(store.items[0]!, {
      platform: 'ebay', listedFor: cents(4000), price: cents(3864), payout: null,
      shippingCharged: cents(500), shippingCost: cents(450), otherCosts: cents(50), date: '2026-09-20',
    })

    expect(api.sell).toHaveBeenCalledWith('a', '"v1"', expect.objectContaining({ price: 38.64, payout: null, shippingCost: 4.5 }))
    expect(store.items[0]!.status).toBe('sold')
  })

  it('on a stale version, says so and reloads rather than overwriting', async () => {
    const store = useItemsStore()
    api.list.mockResolvedValueOnce([itemDto({ id: 'a', title: 'Mine' })])
    await store.load()
    api.update.mockRejectedValue(new ApiError(412, 'Stale version', 'The item changed since you loaded it.'))
    api.list.mockResolvedValueOnce([itemDto({ id: 'a', title: 'Edited on the phone' })])

    await expect(store.update(store.items[0]!, { ...store.items[0]!, title: 'Edited here' })).rejects.toBeInstanceOf(ApiError)

    expect(store.items[0]!.title).toBe('Edited on the phone')
    expect(useToastStore().toasts[0]?.message).toMatch(/changed on another device/)
  })

  it('drops a deleted item', async () => {
    const store = useItemsStore()
    api.list.mockResolvedValue([itemDto({ id: 'a' }), itemDto({ id: 'b' })])
    await store.load()
    api.remove.mockResolvedValue(undefined)

    await store.remove(store.items[0]!)

    expect(store.items.map((i) => i.id)).toEqual(['b'])
  })
})
