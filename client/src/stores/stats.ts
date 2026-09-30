import { defineStore } from 'pinia'
import { ref } from 'vue'
import { statsApi, type DashboardDto, type TrendsDto } from '@/api'
import { getLocalDateString } from '@/domain/dates'
import { readSnapshot, writeSnapshot } from '@/offline/db'
import { isNetworkError } from '@/offline/sync'
import { useAuthStore } from './auth'

/**
 * Dashboard and trends figures, computed by the server (the source of truth for
 * money). Any item change marks them stale; they refetch the next time a page
 * that shows them asks.
 */
export const useStatsStore = defineStore('stats', () => {
  const dashboard = ref<DashboardDto | null>(null)
  const trends = ref<TrendsDto | null>(null)
  /** Bumped on every invalidation, so a page showing stats can watch it and refetch. */
  const revision = ref(0)
  let dashboardFresh = false
  let trendsFresh = false

  function invalidate() {
    dashboardFresh = false
    trendsFresh = false
    revision.value++
  }

  /** When the figures showing were fetched, if they came from this device's copy rather than the server just now. */
  const savedAt = ref<string | null>(null)

  interface Saved<T> {
    value: T
    savedAt: string
  }

  /** From the server, or with no connection, the last figures this device saw. */
  async function fetchOrRecall<T>(name: string, fetch: () => Promise<T>): Promise<T | null> {
    const userId = useAuthStore().me?.id ?? ''
    try {
      const value = await fetch()
      savedAt.value = null
      void writeSnapshot(userId, name, { value, savedAt: new Date().toISOString() } satisfies Saved<T>).catch(() => {})
      return value
    } catch (error) {
      if (!isNetworkError(error)) throw error
      const saved = await readSnapshot<Saved<T>>(userId, name).catch(() => undefined)
      savedAt.value = saved?.savedAt ?? null
      return saved?.value ?? null
    }
  }

  async function loadDashboard() {
    if (dashboardFresh && dashboard.value) return
    dashboard.value = await fetchOrRecall('dashboard', () => statsApi.dashboard(getLocalDateString()))
    dashboardFresh = savedAt.value === null
  }

  async function loadTrends() {
    if (trendsFresh && trends.value) return
    trends.value = await fetchOrRecall('trends', () => statsApi.trends(getLocalDateString()))
    trendsFresh = savedAt.value === null
  }

  function clear() {
    dashboard.value = null
    trends.value = null
    invalidate()
  }

  return { dashboard, trends, savedAt, revision, invalidate, loadDashboard, loadTrends, clear }
})
