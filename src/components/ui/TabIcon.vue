<script setup>
// The four destination icons, shared by the phone tab bar and the desktop
// sidebar. Drawn on a 20×20 grid; the caller picks the rendered size.
defineProps({
  id: { type: String, required: true },
  color: { type: String, required: true },
  size: { type: Number, default: 22 },
})
</script>

<template>
  <svg :width="size" :height="size" viewBox="0 0 20 20" aria-hidden="true">
    <rect v-if="id === 'dashboard'" x="3" y="3" width="14" height="14" rx="3"
      :stroke="color" stroke-width="1.6" fill="none" />
    <g v-else-if="id === 'inventory'" :stroke="color" stroke-width="1.6" fill="none" stroke-linejoin="round">
      <path d="M3 6.5 10 3l7 3.5v7L10 17l-7-3.5z" />
      <path d="M3 6.5 10 10l7-3.5M10 10v7" />
    </g>
    <g v-else-if="id === 'sales'" :stroke="color" stroke-width="1.6" fill="none" stroke-linecap="round">
      <circle cx="10" cy="10" r="6.5" />
      <path d="M12 7.5H9.2a1.6 1.6 0 0 0 0 3.2h1.6a1.6 1.6 0 0 1 0 3.2H8M10 6v1.5M10 13.9v1.5" />
    </g>
    <polyline v-else-if="id === 'analytics'" points="3,14 7,9 11,12 17,5" :stroke="color"
      stroke-width="1.6" fill="none" stroke-linecap="round" stroke-linejoin="round" />
  </svg>
</template>
