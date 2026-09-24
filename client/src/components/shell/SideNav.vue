<script setup lang="ts">
import { useRoute, type RouteLocationResolved } from 'vue-router'
import NavIcon from '../NavIcon.vue'

// Desktop navigation. A phone hides everything behind a four-slot tab bar because
// there is no room; a desktop has a whole column going spare, so the destinations,
// the primary action and the account all stay visible at once.
defineProps<{ tabs: RouteLocationResolved[]; displayName: string; accountLine: string }>()
const emit = defineEmits<{ add: []; signOut: [] }>()
const route = useRoute()
</script>

<template>
  <nav class="side-nav" aria-label="Main">
    <div class="side-nav-brand">
      <span class="app-brand-sub">RESELL TRACKER</span>
    </div>

    <button class="btn btn-primary side-nav-add" @click="emit('add')"><span aria-hidden="true">+</span> New item</button>

    <ul class="side-nav-list">
      <li v-for="tab in tabs" :key="String(tab.name)">
        <RouterLink
          :to="tab"
          :class="['side-nav-item', { active: route.name === tab.name }]"
          :aria-current="route.name === tab.name ? 'page' : undefined"
        >
          <NavIcon :name="tab.meta.icon ?? 'dashboard'" :size="18" />
          <span>{{ tab.meta.title }}</span>
        </RouterLink>
      </li>
    </ul>

    <div class="side-nav-foot">
      <div class="side-nav-account">
        <div class="side-nav-name">{{ displayName || 'You' }}</div>
        <div class="side-nav-mail">{{ accountLine }}</div>
      </div>
      <RouterLink
        :to="{ name: 'settings' }"
        :class="['side-nav-item', { active: route.name === 'settings' }]"
        :aria-current="route.name === 'settings' ? 'page' : undefined"
      >
        <NavIcon name="settings" :size="18" />
        <span>Settings</span>
      </RouterLink>
      <button class="side-nav-item danger" @click="emit('signOut')">
        <NavIcon name="signout" :size="18" />
        <span>Sign out</span>
      </button>
    </div>
  </nav>
</template>

<style scoped>
/* RouterLink renders anchors where the original had buttons; keep them looking the same. */
a.side-nav-item { text-decoration: none; }
</style>
