import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { ApiError, itemsApi, type ItemDto } from '@/api'
import { fromDto, toRequest, type Item, type ItemFields } from '@/domain/item'
import { toDollars, type Cents } from '@/domain/money'
import type { SaleFigures } from '@/domain/profit'
import { useStatsStore } from './stats'
import { useToastStore } from './toast'

export interface SaleInput extends SaleFigures {
  listedFor: Cents
  date: string
}

export interface DonationInput {
  date: string
  org: string
  receiptValue: Cents | null
}

/**
 * The signed-in user's whole inventory, loaded once and kept in step with every
 * change. Filtering and sorting happen on the client, as in the original: it is
 * what makes search instant, and it is what works offline.
 */
export const useItemsStore = defineStore('items', () => {
  const items = ref<Item[]>([])
  const loaded = ref(false)

  const byId = computed(() => new Map(items.value.map((i) => [i.id, i])))

  async function load() {
    items.value = (await itemsApi.list()).map(fromDto)
    loaded.value = true
  }

  function clear() {
    items.value = []
    loaded.value = false
  }

  /** Puts the server's copy of an item in place, newest-first for new ones. */
  function upsert(dto: ItemDto): Item {
    const item = fromDto(dto)
    const index = items.value.findIndex((i) => i.id === item.id)
    if (index === -1) items.value.unshift(item)
    else items.value[index] = item
    useStatsStore().invalidate()
    return item
  }

  /**
   * Runs a change against the server. A stale version means another device got
   * there first: reload so what's on screen is true, and say so.
   */
  async function change(run: () => Promise<ItemDto>): Promise<Item> {
    try {
      return upsert(await run())
    } catch (error) {
      if (error instanceof ApiError && error.isStale) {
        useToastStore().show('That item changed on another device. Showing the latest; try again.', 'error')
        await load()
      }
      throw error
    }
  }

  const create = (fields: ItemFields) => change(() => itemsApi.create(toRequest(fields, crypto.randomUUID())))

  const update = (item: Item, fields: ItemFields) => change(() => itemsApi.update(item.id, item.version, toRequest(fields)))

  const sell = (item: Item, sale: SaleInput) =>
    change(() =>
      itemsApi.sell(item.id, item.version, {
        platform: sale.platform,
        listedFor: toDollars(sale.listedFor),
        price: toDollars(sale.price),
        payout: sale.payout === null ? null : toDollars(sale.payout),
        shippingCharged: toDollars(sale.shippingCharged),
        shippingCost: toDollars(sale.shippingCost),
        otherCosts: toDollars(sale.otherCosts),
        date: sale.date,
      }),
    )

  const undoSale = (item: Item) => change(() => itemsApi.undoSale(item.id, item.version))

  const donate = (item: Item, donation: DonationInput) =>
    change(() =>
      itemsApi.donate(item.id, item.version, {
        ...donation,
        receiptValue: donation.receiptValue === null ? null : toDollars(donation.receiptValue),
      }),
    )

  const undoDonation = (item: Item) => change(() => itemsApi.undoDonation(item.id, item.version))

  const setPhoto = (item: Item, thumbnail: Blob, full: Blob) =>
    change(() => itemsApi.setPhoto(item.id, item.version, thumbnail, full))

  const removePhoto = (item: Item) => change(() => itemsApi.removePhoto(item.id, item.version))

  async function remove(item: Item) {
    await itemsApi.remove(item.id, item.version)
    items.value = items.value.filter((i) => i.id !== item.id)
    useStatsStore().invalidate()
  }

  return {
    items,
    loaded,
    byId,
    load,
    clear,
    create,
    update,
    sell,
    undoSale,
    donate,
    undoDonation,
    setPhoto,
    removePhoto,
    remove,
  }
})
