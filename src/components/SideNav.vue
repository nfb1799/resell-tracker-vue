<script setup>
import TabIcon from './ui/TabIcon.vue'

// Desktop navigation. A phone hides everything behind a four-slot tab bar
// because there is no room; a desktop has a whole column going spare, so the
// destinations, the primary action and the account all stay visible at once.
defineProps({
  tabs: { type: Array, required: true },
  page: { type: String, required: true },
  displayName: { type: String, default: '' },
  email: { type: String, default: '' },
})
const emit = defineEmits(['navigate', 'add-item', 'bulk-import', 'logout'])
</script>

<template>
  <nav class="side-nav" aria-label="Main">
    <div class="side-nav-brand">
      <span class="app-brand-sub">RESELL TRACKER</span>
    </div>

    <button class="btn btn-primary side-nav-add" @click="emit('add-item')">
      <span aria-hidden="true">+</span> New item
    </button>

    <button class="btn btn-sm side-nav-import" @click="emit('bulk-import')">
      Bulk import
    </button>

    <ul class="side-nav-list">
      <li v-for="tab in tabs" :key="tab.id">
        <button
          :class="['side-nav-item', { active: tab.id === page }]"
          :aria-current="tab.id === page ? 'page' : undefined"
          @click="emit('navigate', tab.id)"
        >
          <TabIcon :id="tab.id" :size="18"
            :color="tab.id === page ? 'var(--accent-primary)' : 'var(--text-muted)'" />
          <span>{{ tab.label }}</span>
        </button>
      </li>
    </ul>

    <div class="side-nav-foot">
      <div class="side-nav-account">
        <div class="side-nav-name">{{ displayName || 'You' }}</div>
        <div class="side-nav-mail">{{ email || 'Guest account' }}</div>
      </div>
      <button
        :class="['side-nav-item', { active: page === 'settings' }]"
        :aria-current="page === 'settings' ? 'page' : undefined"
        @click="emit('navigate', 'settings')"
      >
        <svg width="18" height="18" viewBox="0 0 20 20" aria-hidden="true">
          <circle cx="10" cy="10" r="2.6" stroke="currentColor" stroke-width="1.5" fill="none" />
          <path d="M10 2.6v2M10 15.4v2M17.4 10h-2M4.6 10h-2M15.2 4.8l-1.4 1.4M6.2 13.8l-1.4 1.4M15.2 15.2l-1.4-1.4M6.2 6.2 4.8 4.8"
            stroke="currentColor" stroke-width="1.5" stroke-linecap="round" />
        </svg>
        <span>Settings</span>
      </button>
      <button class="side-nav-item danger" @click="emit('logout')">
        <svg width="18" height="18" viewBox="0 0 20 20" aria-hidden="true">
          <path d="M12 6V4.5a1.5 1.5 0 0 0-1.5-1.5h-5A1.5 1.5 0 0 0 4 4.5v11A1.5 1.5 0 0 0 5.5 17h5a1.5 1.5 0 0 0 1.5-1.5V14"
            stroke="currentColor" stroke-width="1.5" fill="none" stroke-linecap="round" stroke-linejoin="round" />
          <path d="M8.5 10h8m0 0-2.4-2.4M16.5 10l-2.4 2.4"
            stroke="currentColor" stroke-width="1.5" fill="none" stroke-linecap="round" stroke-linejoin="round" />
        </svg>
        <span>Sign out</span>
      </button>
    </div>
  </nav>
</template>
