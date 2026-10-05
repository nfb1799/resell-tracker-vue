<script setup>
import { ref, computed, watch, defineAsyncComponent, h } from 'vue'
import '../App.css'
import '../desktop.css'
import { useAuth } from '../composables/useAuth'
import { useItems } from '../composables/useItems'
import { useIsDesktop } from '../composables/useMediaQuery'
import DashboardPage from './DashboardPage.vue'
import InventoryPage from './InventoryPage.vue'
import SalesPage from './SalesPage.vue'
import SettingsPage from './SettingsPage.vue'
import ItemSheet from './ItemSheet.vue'
import SellSheet from './SellSheet.vue'
import DonateSheet from './DonateSheet.vue'
import BulkImportSheet from './BulkImportSheet.vue'
import OfflineIndicator from './OfflineIndicator.vue'
import SideNav from './SideNav.vue'
import TabIcon from './ui/TabIcon.vue'

// Chart.js is a big dependency and only the Trends tab needs it.
const AnalyticsPage = defineAsyncComponent({
  loader: () => import('./AnalyticsPage.vue'),
  loadingComponent: { render: () => h('p', { class: 'muted' }, 'Loading charts…') },
})

const PAGES = {
  dashboard: { title: 'Overview', sub: 'Where the money stands right now' },
  inventory: { title: 'Inventory', sub: 'Everything you hold, listed or sold' },
  sales: { title: 'Sales', sub: 'Every sale and what it actually returned' },
  analytics: { title: 'Trends', sub: 'How the numbers move over time' },
  settings: { title: 'Settings', sub: 'Currency, fee estimates and exports' },
}

const TABS = [
  { id: 'dashboard', label: 'Overview' },
  { id: 'inventory', label: 'Inventory' },
  { id: 'sales', label: 'Sales' },
  { id: 'analytics', label: 'Trends' },
]

const { currentUser, userProfile, logout } = useAuth()
const { settings } = useItems()
const isDesktop = useIsDesktop()
const page = ref('dashboard')
const profileMenuOpen = ref(false)
// One sheet at a time: { mode: 'new' | 'edit' | 'sell' | 'donate' | 'import', item }
const sheet = ref(null)

// The saved theme follows the account, so a second device picks it up once
// the profile arrives rather than sticking with this device's last choice.
watch(() => settings.value.theme, (theme) => {
  if (theme) {
    document.documentElement.setAttribute('data-theme', theme)
    localStorage.setItem('theme', theme)
  }
}, { immediate: true })

function goTo(next) {
  page.value = next
  profileMenuOpen.value = false
}

async function handleLogout() {
  try {
    await logout()
    page.value = 'dashboard'
    profileMenuOpen.value = false
  } catch (error) {
    console.error('Logout error:', error)
  }
}

// Tapping a finished item goes straight to how it ended; anything still on
// the shelf opens the editor, which can hand off to either outcome.
const openItem = (item) => {
  sheet.value = {
    mode: item.status === 'sold' ? 'sell' : item.status === 'donated' ? 'donate' : 'edit',
    item,
  }
}
const sellItem = (item) => { sheet.value = { mode: 'sell', item } }
const donateItem = (item) => { sheet.value = { mode: 'donate', item } }
const editItem = (item) => { sheet.value = { mode: 'edit', item } }
const addItem = () => { sheet.value = { mode: 'new', item: null } }
const bulkImport = () => { sheet.value = { mode: 'import', item: null } }
const closeSheet = () => { sheet.value = null }

const initials = computed(() =>
  (userProfile.value?.displayName || currentUser.value?.email || 'U').trim().charAt(0).toUpperCase())

const sheetKey = computed(() => (sheet.value ? `${sheet.value.mode}:${sheet.value.item?.id ?? ''}` : ''))
</script>

<template>
  <div :class="['app', { 'app--desktop': isDesktop }]">
    <SideNav
      v-if="isDesktop"
      :tabs="TABS"
      :page="page"
      :display-name="userProfile?.displayName"
      :email="currentUser?.email"
      @navigate="goTo"
      @add-item="addItem"
      @bulk-import="bulkImport"
      @logout="handleLogout"
    />
    <header v-else class="app-header">
      <div>
        <span class="app-brand-sub">RESELL TRACKER</span>
        <h1 class="app-brand">{{ PAGES[page].title }}</h1>
      </div>
      <button class="app-avatar" aria-label="Profile menu" @click="profileMenuOpen = !profileMenuOpen">
        {{ initials }}
      </button>
    </header>

    <div v-if="isDesktop" class="page-header">
      <h1>{{ PAGES[page].title }}</h1>
      <span class="page-header-sub">{{ PAGES[page].sub }}</span>
    </div>

    <template v-if="!isDesktop && profileMenuOpen">
      <div class="app-overlay" @click="profileMenuOpen = false" />
      <div class="profile-menu">
        <div class="profile-menu-head">
          <div class="profile-menu-name">{{ userProfile?.displayName || 'You' }}</div>
          <div class="profile-menu-mail">{{ currentUser?.email || 'Guest account' }}</div>
        </div>
        <button class="profile-menu-item" @click="goTo('settings')">Settings</button>
        <button class="profile-menu-item danger" @click="handleLogout">Sign out</button>
      </div>
    </template>

    <main :class="['main-content', `page-${page}`]">
      <DashboardPage v-if="page === 'dashboard'" @open="openItem" @sell="sellItem" @add="addItem" />
      <InventoryPage v-else-if="page === 'inventory'"
        @open="openItem" @sell="sellItem" @add="addItem" @bulk-import="bulkImport" />
      <SalesPage v-else-if="page === 'sales'" @open="openItem" />
      <AnalyticsPage v-else-if="page === 'analytics'" />
      <SettingsPage v-else-if="page === 'settings'" />
    </main>

    <BulkImportSheet v-if="sheet?.mode === 'import'" @close="closeSheet" />
    <SellSheet v-else-if="sheet?.mode === 'sell'" :key="sheetKey" :item="sheet.item"
      @close="closeSheet" @edit-details="editItem" />
    <DonateSheet v-else-if="sheet?.mode === 'donate'" :key="sheetKey" :item="sheet.item" @close="closeSheet" />
    <ItemSheet v-else-if="sheet" :key="sheetKey" :item="sheet.item"
      @close="closeSheet" @sell="sellItem" @donate="donateItem" />

    <button v-if="!isDesktop && !sheet && page !== 'settings'" class="fab" aria-label="Add item" @click="addItem">
      +
    </button>

    <nav v-if="!isDesktop" class="bottom-tabs">
      <button
        v-for="tab in TABS"
        :key="tab.id"
        :class="['bottom-tab', { active: tab.id === page }]"
        :style="{ color: tab.id === page ? 'var(--accent-primary)' : 'var(--text-dimmed)' }"
        :aria-current="tab.id === page ? 'page' : undefined"
        @click="goTo(tab.id)"
      >
        <TabIcon :id="tab.id" :color="tab.id === page ? 'var(--accent-primary)' : 'var(--text-dimmed)'" />
        <span>{{ tab.label }}</span>
      </button>
    </nav>

    <OfflineIndicator />
  </div>
</template>
