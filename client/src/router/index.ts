import { createRouter, createWebHistory } from 'vue-router'
import type { NavIconName } from '@/components/NavIcon.vue'
import { useAuthStore } from '@/stores/auth'
import DashboardView from '@/views/DashboardView.vue'
import InventoryView from '@/views/InventoryView.vue'
import SalesView from '@/views/SalesView.vue'
import SettingsView from '@/views/SettingsView.vue'
import SignInView from '@/views/SignInView.vue'

declare module 'vue-router' {
  interface RouteMeta {
    /** Reachable signed out (the sign-in and reset pages); everything else needs a session. */
    public?: boolean
    title?: string
    sub?: string
    icon?: NavIconName
  }
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/sign-in', name: 'sign-in', component: SignInView, meta: { public: true } },
    {
      path: '/reset-password',
      name: 'reset-password',
      component: () => import('@/views/ResetPasswordView.vue'),
      meta: { public: true },
    },
    {
      path: '/',
      name: 'dashboard',
      component: DashboardView,
      meta: { title: 'Overview', sub: 'Where the money stands right now', icon: 'dashboard' },
    },
    {
      path: '/inventory',
      name: 'inventory',
      component: InventoryView,
      meta: { title: 'Inventory', sub: 'Everything you hold, listed or sold', icon: 'inventory' },
    },
    {
      path: '/sales',
      name: 'sales',
      component: SalesView,
      meta: { title: 'Sales', sub: 'Every sale and what it actually returned', icon: 'sales' },
    },
    {
      path: '/trends',
      name: 'trends',
      // Chart.js is the biggest dependency and only this page needs it.
      component: () => import('@/views/TrendsView.vue'),
      meta: { title: 'Trends', sub: 'How the numbers move over time', icon: 'trends' },
    },
    {
      path: '/settings',
      name: 'settings',
      component: SettingsView,
      meta: { title: 'Settings', sub: 'Currency, fee estimates and exports' },
    },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  await auth.ensureChecked()
  if (!to.meta.public && !auth.signedIn) {
    return { name: 'sign-in', query: to.fullPath === '/' ? {} : { next: to.fullPath } }
  }
  if (to.name === 'sign-in' && auth.signedIn) return { name: 'dashboard' }
  return true
})

/** The four destinations in the tab bar and sidebar, in order. */
export const NAV_ROUTES = ['dashboard', 'inventory', 'sales', 'trends'] as const

export default router
