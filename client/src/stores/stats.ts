import { defineStore } from 'pinia'
import { ref } from 'vue'
import { statsApi, type DashboardDto, type TrendsDto } from '@/api'
import { getLocalDateString } from '@/domain/dates'

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

  async function loadDashboard() {
    if (dashboardFresh && dashboard.value) return
    dashboard.value = await statsApi.dashboard(getLocalDateString())
    dashboardFresh = true
  }

  async function loadTrends() {
    if (trendsFresh && trends.value) return
    trends.value = await statsApi.trends(getLocalDateString())
    trendsFresh = true
  }

  function clear() {
    dashboard.value = null
    trends.value = null
    invalidate()
  }

  return { dashboard, trends, revision, invalidate, loadDashboard, loadTrends, clear }
})
