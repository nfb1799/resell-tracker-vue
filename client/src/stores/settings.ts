import { defineStore } from 'pinia'
import { computed, ref, watch } from 'vue'
import { settingsApi, type SettingsDto } from '@/api'
import { fromDollars, toDollars, type Cents } from '@/domain/money'
import { withFeeDefaults, type FeeSettings } from '@/domain/platforms'
import { readSnapshot, writeSnapshot } from '@/offline/db'
import { isNetworkError } from '@/offline/sync'
import { useAuthStore } from './auth'

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

  interface Saved {
    general: SettingsDto
    fees: FeeSettings
  }

  const userId = () => useAuthStore().me?.id ?? ''
  const remember = () => void writeSnapshot(userId(), 'settings', { general: general.value, fees: fees.value }).catch(() => {})

  /** From the server, or with no connection, from what this device saved last. */
  async function load() {
    try {
      const [saved, savedFees] = await Promise.all([settingsApi.get(), settingsApi.getFees()])
      general.value = saved
      fees.value = withFeeDefaults(savedFees)
      remember()
    } catch (error) {
      if (!isNetworkError(error)) throw error
      const saved = await readSnapshot<Saved>(userId(), 'settings').catch(() => undefined)
      if (saved) {
        general.value = saved.general
        fees.value = withFeeDefaults(saved.fees)
      }
    }
  }

  async function save(changes: Partial<SettingsDto> & { profitGoalCents?: Cents }) {
    const { profitGoalCents, ...rest } = changes
    const next = { ...general.value, ...rest }
    if (profitGoalCents !== undefined) next.profitGoal = toDollars(profitGoalCents)
    const previous = general.value
    general.value = next // optimistic, so the theme flips at once
    try {
      general.value = await settingsApi.save(next)
      remember()
    } catch (error) {
      general.value = previous // settings save online only; don't pretend otherwise
      throw error
    }
  }

  async function saveFees(next: FeeSettings) {
    fees.value = withFeeDefaults(await settingsApi.saveFees(next))
    remember()
  }

  /** Back to the registry's defaults: saving nothing removes every override. */
  async function resetFees() {
    fees.value = withFeeDefaults(await settingsApi.saveFees({}))
    remember()
  }

  function clear() {
    general.value = { ...DEFAULTS, theme: general.value.theme }
    fees.value = withFeeDefaults()
  }

  return { general, fees, currency, profitGoal, load, save, saveFees, resetFees, clear }
})
