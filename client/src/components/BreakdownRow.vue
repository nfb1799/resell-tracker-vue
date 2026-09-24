<script setup lang="ts">
defineProps<{ label: string; value: string; tone?: 'pos' | 'neg'; total?: boolean }>()
</script>

<template>
  <div :class="['breakdown-row', { total }]">
    <span class="breakdown-label">{{ label }}</span>
    <span :class="['breakdown-value', tone]">{{ value }}</span>
  </div>
</template>
