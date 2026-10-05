<script setup>
defineProps({
  label: { type: String, required: true },
  value: { type: String, required: true },
  sub: { type: String, default: '' },
  tone: { type: String, default: '' },
  wide: Boolean,
  accent: Boolean,
})
</script>

<template>
  <div :class="['stat-tile', { wide, accent }]">
    <span class="stat-label">{{ label }}</span>
    <span :class="['stat-value', tone]">{{ value }}</span>
    <span v-if="sub" class="stat-sub">{{ sub }}</span>
  </div>
</template>
