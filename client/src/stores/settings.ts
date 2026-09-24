import { defineStore } from 'pinia'
import { computed, ref, watch } from 'vue'
import { settingsApi, type SettingsDto } from '@/api'
import { fromDollars, toDollars, type Cents } from '@/domain/money'
import { withFeeDefaults, type FeeSettings } from '@/domain/platforms'

const DEFAULTS: SettingsDto = { displayName: '', currency: 'USD', theme: 'dark', profitGoal: 0 }

export const CURRENCIES = ['USD', 'GBP', 'EUR', 'CAD', 'AUD'] as const

export const useSettingsStore = defineStore('settings', () => {
  const general = ref<SettingsDto>({ ...DEFAULTS })
  const fees = ref<FeeSettings>(withFeeDefaults())

  const currency = computed(() => general.value.currency)
  const profitGoal = computed<Cents>(() => fromDollars(general.value.profitGoal))

  // The saved theme follows the account, so another device picks it up on
  // sign-in; localStorage just avoids a flash before that (see index.html).
  watch(
    () => general.value.theme,
    (theme) => {
      document.documentElement.dataset.theme = theme
      try {
        localStorage.setItem('theme', theme)
      } catch {
        // Private mode and the like: the theme still applies for this visit.
      }
    },
  )

  async function load() {
    const [saved, savedFees] = await Promise.all([settingsApi.get(), settingsApi.getFees()])
    general.value = saved
    fees.value = withFeeDefaults(savedFees)
  }

  async function save(changes: Partial<SettingsDto> & { profitGoalCents?: Cents }) {
    const { profitGoalCents, ...rest } = changes
    const next = { ...general.value, ...rest }
    if (profitGoalCents !== undefined) next.profitGoal = toDollars(profitGoalCents)
    general.value = next // optimistic, so the theme flips at once
    general.value = await settingsApi.save(next)
  }

  async function saveFees(next: FeeSettings) {
    fees.value = withFeeDefaults(await settingsApi.saveFees(next))
  }

  /** Back to the registry's defaults: saving nothing removes every override. */
  async function resetFees() {
    fees.value = withFeeDefaults(await settingsApi.saveFees({}))
  }

  function clear() {
    general.value = { ...DEFAULTS, theme: general.value.theme }
    fees.value = withFeeDefaults()
  }

  return { general, fees, currency, profitGoal, load, save, saveFees, resetFees, clear }
})
