// The marketplaces an item can be listed on or sold through. The list itself is
// shared/platforms.json, which the server embeds too, so adding a platform is one
// entry there plus a --<id>-color token in both themes.

import registry from '@shared/platforms.json'

/**
 * fee = base * percent / 100 + fixed, where base is the price alone, or price +
 * shipping charged when `includesShipping`. Dollars and percent, as the API sends
 * them; the profit math converts to cents.
 */
export interface FeeSchedule {
  percent: number
  fixed: number
  includesShipping: boolean
}

export interface Platform {
  id: string
  label: string
  note: string
  defaultFees: FeeSchedule
}

export type FeeSettings = Record<string, FeeSchedule>

export const PLATFORMS: readonly Platform[] = registry.platforms
export const PLATFORM_IDS: readonly string[] = PLATFORMS.map((p) => p.id)
export const FALLBACK_PLATFORM: string = registry.fallback

const byId = new Map(PLATFORMS.map((p) => [p.id, p]))

export const findPlatform = (id: string | null | undefined): Platform | undefined =>
  id ? byId.get(id) : undefined

export const platformLabel = (id: string | null | undefined): string =>
  findPlatform(id)?.label ?? id ?? '—'

export function defaultFeeSettings(): FeeSettings {
  return Object.fromEntries(PLATFORMS.map((p) => [p.id, { ...p.defaultFees }]))
}

/**
 * A user's saved schedules laid over the defaults, so a platform added after
 * they last saved still has one and callers can index straight into the result.
 * Unknown platforms are dropped.
 */
export function withFeeDefaults(saved?: Partial<Record<string, Partial<FeeSchedule>>> | null): FeeSettings {
  const settings = defaultFeeSettings()
  for (const id of PLATFORM_IDS) {
    settings[id] = { ...settings[id]!, ...saved?.[id] }
  }
  return settings
}

/** The schedule for a platform, or the fallback platform's for an unknown id. */
export const scheduleFor = (fees: FeeSettings, platform: string | null | undefined): FeeSchedule =>
  (platform ? fees[platform] : undefined) ?? fees[FALLBACK_PLATFORM]!
