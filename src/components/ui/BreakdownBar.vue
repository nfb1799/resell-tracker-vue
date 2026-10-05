<script setup>
import { formatMoney } from '../../lib/money'

// A labelled proportional bar — identity comes from the written label, colour
// only reinforces it.
defineProps({
  label: { type: String, required: true },
  value: { type: Number, required: true },
  share: { type: Number, required: true },
  color: { type: String, default: '' },
  currency: { type: String, required: true },
  note: { type: String, default: '' },
})
</script>

<template>
  <div style="display: flex; flex-direction: column; gap: 4px">
    <div style="display: flex; justify-content: space-between; gap: 10px; font-size: 13px">
      <span style="font-weight: 600">{{ label }}</span>
      <span class="mono" style="font-weight: 600">
        {{ formatMoney(value, currency) }}<span v-if="note" class="dimmed" style="font-weight: 400"> · {{ note }}</span>
      </span>
    </div>
    <div style="height: 8px; border-radius: 4px; background: var(--bg-tertiary); overflow: hidden">
      <div :style="{
        width: `${Math.max(0, Math.min(1, share)) * 100}%`,
        height: '100%',
        borderRadius: '4px',
        background: color,
      }" />
    </div>
  </div>
</template>
