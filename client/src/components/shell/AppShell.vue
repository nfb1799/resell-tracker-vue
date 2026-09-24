<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useIsDesktop } from '@/composables/useMediaQuery'
import { useSignOut } from '@/composables/useSignOut'
import { NAV_ROUTES } from '@/router'
import { useAuthStore } from '@/stores/auth'
import { useItemsStore } from '@/stores/items'
import { useSettingsStore } from '@/stores/settings'
import { useSheetStore } from '@/stores/sheet'
import { useToastStore } from '@/stores/toast'
import NavIcon from '../NavIcon.vue'
import SheetHost from '../sheets/SheetHost.vue'
import SideNav from './SideNav.vue'

// The signed-in app: a sidebar on desktop, a header and bottom tab bar on a phone.
// Only one of the two structures is ever in the DOM (see useIsDesktop).
const route = useRoute()
const router = useRouter()
const isDesktop = useIsDesktop()
const auth = useAuthStore()
const items = useItemsStore()
const settings = useSettingsStore()
const sheet = useSheetStore()
const toast = useToastStore()
const signOut = useSignOut()

const menuOpen = ref(false)
const tabs = computed(() => NAV_ROUTES.map((name) => router.resolve({ name })))
const displayName = computed(() => settings.general.displayName || auth.me?.displayName || '')
const initials = computed(() => (displayName.value || auth.me?.email || 'U').trim().charAt(0).toUpperCase())
const accountLine = computed(() => (auth.me?.isDemo ? 'Demo account' : (auth.me?.email ?? '')))

onMounted(async () => {
  try {
    await Promise.all([items.loaded ? null : items.load(), settings.load()])
  } catch {
    toast.show('Could not load your inventory. Check your connection and reload.', 'error')
  }
})

function go(name: string) {
  menuOpen.value = false
  void router.push({ name })
}
</script>

<template>
  <div :class="['app', { 'app--desktop': isDesktop }]">
    <SideNav
      v-if="isDesktop"
      :tabs="tabs"
      :display-name="displayName"
      :account-line="accountLine"
      @add="sheet.open('new')"
      @sign-out="signOut"
    />
    <header v-else class="app-header">
      <div>
        <span class="app-brand-sub">RESELL TRACKER</span>
        <h1 class="app-brand">{{ route.meta.title }}</h1>
      </div>
      <button class="app-avatar" aria-label="Profile menu" :aria-expanded="menuOpen" @click="menuOpen = !menuOpen">
        {{ initials }}
      </button>
    </header>

    <div v-if="isDesktop" class="page-header">
      <h1>{{ route.meta.title }}</h1>
      <span class="page-header-sub">{{ route.meta.sub }}</span>
    </div>

    <template v-if="!isDesktop && menuOpen">
      <div class="app-overlay" @click="menuOpen = false" />
      <div class="profile-menu">
        <div class="profile-menu-head">
          <div class="profile-menu-name">{{ displayName || 'You' }}</div>
          <div class="profile-menu-mail">{{ accountLine }}</div>
        </div>
        <button class="profile-menu-item" @click="go('settings')">Settings</button>
        <button class="profile-menu-item danger" @click="signOut">Sign out</button>
      </div>
    </template>

    <main :class="['main-content', `page-${String(route.name)}`]">
      <RouterView />
    </main>

    <SheetHost />

    <button v-if="!isDesktop && !sheet.mode && route.name !== 'settings'" class="fab" aria-label="Add item" @click="sheet.open('new')">+</button>

    <nav v-if="!isDesktop" class="bottom-tabs" aria-label="Main">
      <RouterLink
        v-for="tab in tabs"
        :key="String(tab.name)"
        :to="tab"
        :class="['bottom-tab', { active: route.name === tab.name }]"
        :aria-current="route.name === tab.name ? 'page' : undefined"
      >
        <NavIcon :name="tab.meta.icon ?? 'dashboard'" :size="22" />
        <span>{{ tab.meta.title }}</span>
      </RouterLink>
    </nav>
  </div>
</template>

<style scoped>
/* The original colours inactive tabs dimmed and the active one with the accent. */
.bottom-tab { color: var(--text-dimmed); text-decoration: none; }
.bottom-tab.active { color: var(--accent-primary); }
</style>
