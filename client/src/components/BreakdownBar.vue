<script setup lang="ts">
// A labelled proportional bar. Identity comes from the written label; colour
// only reinforces it.
defineProps<{ label: string; value: string; share: number; color: string; note?: string }>()
</script>

<template>
  <div class="breakdown-bar">
    <div class="breakdown-bar-head">
      <span class="breakdown-bar-label">{{ label }}</span>
      <span class="mono breakdown-bar-value">
        {{ value }}<span v-if="note" class="dimmed"> · {{ note }}</span>
      </span>
    </div>
    <div class="breakdown-bar-track">
      <div class="breakdown-bar-fill" :style="{ width: `${Math.max(0, Math.min(1, share)) * 100}%`, background: color }" />
    </div>
  </div>
</template>

<style scoped>
.breakdown-bar { display: flex; flex-direction: column; gap: 4px; }
.breakdown-bar-head { display: flex; justify-content: space-between; gap: 10px; font-size: 13px; }
.breakdown-bar-label, .breakdown-bar-value { font-weight: 600; }
.breakdown-bar-value .dimmed { font-weight: 400; }
.breakdown-bar-track { height: 8px; border-radius: 4px; background: var(--bg-tertiary); overflow: hidden; }
.breakdown-bar-fill { height: 100%; border-radius: 4px; }
</style>
