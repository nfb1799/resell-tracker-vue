<script setup lang="ts">
// The four destinations' icons, drawn in currentColor so the active state is
// just a colour change on the button.
export type NavIconName = 'dashboard' | 'inventory' | 'sales' | 'trends' | 'settings' | 'signout'

defineProps<{ name: NavIconName; size?: number }>()
</script>

<template>
  <svg :width="size ?? 20" :height="size ?? 20" viewBox="0 0 20 20" aria-hidden="true" fill="none" stroke="currentColor">
    <rect v-if="name === 'dashboard'" x="3" y="3" width="14" height="14" rx="3" stroke-width="1.6" />
    <g v-else-if="name === 'inventory'" stroke-width="1.6" stroke-linejoin="round">
      <path d="M3 6.5 10 3l7 3.5v7L10 17l-7-3.5z" />
      <path d="M3 6.5 10 10l7-3.5M10 10v7" />
    </g>
    <g v-else-if="name === 'sales'" stroke-width="1.6" stroke-linecap="round">
      <circle cx="10" cy="10" r="6.5" />
      <path d="M12 7.5H9.2a1.6 1.6 0 0 0 0 3.2h1.6a1.6 1.6 0 0 1 0 3.2H8M10 6v1.5M10 13.9v1.5" />
    </g>
    <polyline v-else-if="name === 'trends'" points="3,14 7,9 11,12 17,5" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" />
    <g v-else-if="name === 'settings'" stroke-width="1.5" stroke-linecap="round">
      <circle cx="10" cy="10" r="2.6" />
      <path d="M10 2.6v2M10 15.4v2M17.4 10h-2M4.6 10h-2M15.2 4.8l-1.4 1.4M6.2 13.8l-1.4 1.4M15.2 15.2l-1.4-1.4M6.2 6.2 4.8 4.8" />
    </g>
    <g v-else stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
      <path d="M12 6V4.5a1.5 1.5 0 0 0-1.5-1.5h-5A1.5 1.5 0 0 0 4 4.5v11A1.5 1.5 0 0 0 5.5 17h5a1.5 1.5 0 0 0 1.5-1.5V14" />
      <path d="M8.5 10h8m0 0-2.4-2.4M16.5 10l-2.4 2.4" />
    </g>
  </svg>
</template>
