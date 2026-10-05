<script setup>
// Small line icons for stat chips and panel headers. Drawn on a 20×20 grid in
// currentColor, so they take the colour of whatever holds them.
defineProps({
  name: { type: String, required: true },
  size: { type: Number, default: 14 },
})
</script>

<template>
  <svg :width="size" :height="size" viewBox="0 0 20 20" fill="none" stroke="currentColor"
    stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
    <template v-if="name === 'wallet'">
      <path d="M3 6.5A2.5 2.5 0 0 1 5.5 4H15v3" />
      <rect x="3" y="6.5" width="14" height="9.5" rx="2" />
      <circle cx="13.5" cy="11.2" r="1" fill="currentColor" stroke="none" />
    </template>
    <template v-else-if="name === 'percent'">
      <path d="M15 5 5 15" />
      <circle cx="6" cy="6" r="1.8" />
      <circle cx="14" cy="14" r="1.8" />
    </template>
    <template v-else-if="name === 'clock'">
      <circle cx="10" cy="10" r="7" />
      <path d="M10 6v4l2.5 2" />
    </template>
    <template v-else-if="name === 'box'">
      <path d="M3 6.5 10 3l7 3.5v7L10 17l-7-3.5z" />
      <path d="M3 6.5 10 10l7-3.5M10 10v7" />
    </template>
    <template v-else-if="name === 'tag'">
      <path d="M3 10.2V4a1 1 0 0 1 1-1h6.2l6.6 6.6a1 1 0 0 1 0 1.4l-5.8 5.8a1 1 0 0 1-1.4 0z" />
      <circle cx="7" cy="7" r="1.2" />
    </template>
    <template v-else-if="name === 'trend'">
      <path d="M3 14l4.5-4.5 3 3L17 6" />
      <path d="M12.5 6H17v4.5" />
    </template>
    <template v-else-if="name === 'gift'">
      <rect x="3" y="7.5" width="14" height="9" rx="1.5" />
      <path d="M10 7.5v9M3 11h14M10 7.5S8.8 3.5 6.8 4.2C5 4.8 6.2 7.5 10 7.5zm0 0s1.2-4 3.2-3.3c1.8.6.6 3.3-3.2 3.3z" />
    </template>
    <template v-else-if="name === 'target'">
      <circle cx="10" cy="10" r="7" />
      <circle cx="10" cy="10" r="3.5" />
      <circle cx="10" cy="10" r=".8" fill="currentColor" />
    </template>
  </svg>
</template>
