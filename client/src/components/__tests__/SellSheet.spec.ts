import { beforeEach, describe, it, expect, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { item, itemDto } from '@/__tests__/fixtures'
import { itemsApi } from '@/api'
import SellSheet from '../sheets/SellSheet.vue'

vi.mock('@/api', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api')>()
  return { ...actual, itemsApi: { sell: vi.fn<typeof actual.itemsApi.sell>() } }
})

const listed = item({ title: 'Docs', status: 'listed', cost: 30, platforms: ['depop'], listPrice: 110, listedDate: '2026-09-06' })

function mountSheet() {
  return mount(SellSheet, { props: { item: listed } })
}

const rows = (wrapper: ReturnType<typeof mountSheet>) =>
  Object.fromEntries(wrapper.findAll('.breakdown-row').map((r) => [r.find('.breakdown-label').text(), r.find('.breakdown-value').text()]))

describe('SellSheet', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.resetAllMocks()
  })

  it('opens prefilled with the asking price and the platform it is listed on', () => {
    const wrapper = mountSheet()

    expect((wrapper.find('#sale-listed').element as HTMLInputElement).value).toBe('110')
    expect((wrapper.find('#sale-price').element as HTMLInputElement).value).toBe('110')
    expect(wrapper.find('.chip.active').text()).toBe('Depop')
  })

  it('updates the breakdown as you type, estimating fees until the payout is known', async () => {
    const wrapper = mountSheet()

    await wrapper.find('#sale-price').setValue('95')
    await wrapper.find('#ship-charged').setValue('8')
    await wrapper.find('#ship-cost').setValue('7.5')

    // Depop: (95 + 8) x 3.3% + 0.45 = 3.849, rounded to 3.85
    expect(rows(wrapper)).toMatchObject({
      'Came down by': '$15.00 · 14%',
      'Depop kept (est.)': '-$3.85',
      'You got paid (est.)': '$99.15',
      'Net profit': '$61.65',
    })

    await wrapper.find('#sale-payout').setValue('100')

    expect(rows(wrapper)).toMatchObject({ 'Depop kept': '-$3.00', 'You got paid': '$100.00', 'Net profit': '$62.50' })
  })

  it('flags a payout above what the buyer paid', async () => {
    const wrapper = mountSheet()

    await wrapper.find('#sale-payout').setValue('150')

    expect(wrapper.find('.inline-warning').exists()).toBe(true)
  })

  it('refuses to save without an accepted offer', async () => {
    const wrapper = mountSheet()

    await wrapper.find('#sale-price').setValue('')
    await wrapper.findAll('.sheet-actions button').at(1)!.trigger('click')

    expect(itemsApi.sell).not.toHaveBeenCalled()
  })

  it('saves the figures in dollars, with a blank payout left as unknown', async () => {
    vi.mocked(itemsApi.sell).mockResolvedValue(itemDto({ id: listed.id, status: 'sold' }))
    const wrapper = mountSheet()

    await wrapper.find('#sale-price').setValue('95')
    await wrapper.findAll('.sheet-actions button').at(1)!.trigger('click')
    await flushPromises()

    expect(itemsApi.sell).toHaveBeenCalledWith(
      listed.id,
      listed.version,
      expect.objectContaining({ platform: 'depop', listedFor: 110, price: 95, payout: null }),
    )
  })
})
