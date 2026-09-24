<script setup lang="ts">
defineProps<{
  label: string
  value: string
  sub?: string
  tone?: 'pos' | 'neg'
  wide?: boolean
  accent?: boolean
}>()
</script>

<template>
  <div :class="['stat-tile', { wide, accent }]">
    <span class="stat-label">{{ label }}</span>
    <span :class="['stat-value', tone]">{{ value }}</span>
    <span v-if="sub" class="stat-sub">{{ sub }}</span>
  </div>
</template>
