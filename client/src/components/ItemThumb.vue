<script setup lang="ts">
// The inline thumbnail that comes with every item, or a placeholder tag icon.
defineProps<{ thumbnail: string | null }>()
</script>

<template>
  <!-- No lazy loading: the thumbnail is already inline, so there is no fetch to defer. -->
  <img v-if="thumbnail" class="item-thumb" :src="thumbnail" alt="" />
  <span v-else class="item-thumb" aria-hidden="true">
    <svg width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.5">
      <path d="M3 7.2 9.4 3l7.6 3.4v6.4L10 17l-7-3.6z" stroke-linejoin="round" />
      <circle cx="7.4" cy="7.6" r="1.3" />
    </svg>
  </span>
</template>
