<script setup>
defineProps({
  label: { type: String, required: true },
  value: { type: String, required: true },
  tone: { type: String, default: '' },
  total: Boolean,
})
</script>

<template>
  <div :class="['breakdown-row', { total }]">
    <span class="breakdown-label">{{ label }}</span>
    <span :class="['breakdown-value', tone]">{{ value }}</span>
  </div>
</template>
