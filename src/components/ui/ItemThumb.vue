<script setup>
// The inline thumbnail stored on the item document, or a placeholder tag icon.
defineProps({ item: { type: Object, required: true } })
</script>

<template>
  <!-- No lazy loading: the thumbnail is already inline in the document, so
       there is no fetch to defer — deferring only delays the decode. -->
  <img v-if="item.thumb" class="item-thumb" :src="item.thumb" alt="" />
  <span v-else class="item-thumb" aria-hidden="true">
    <svg width="20" height="20" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.5">
      <path d="M3 7.2 9.4 3l7.6 3.4v6.4L10 17l-7-3.6z" stroke-linejoin="round" />
      <circle cx="7.4" cy="7.6" r="1.3" />
    </svg>
  </span>
</template>
