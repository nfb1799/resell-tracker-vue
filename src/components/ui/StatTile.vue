<script setup>
import UiIcon from './UiIcon.vue'

// A headline number: icon chip and label, the figure, then a line of context.
// `delta` is an optional leading fragment of that line, coloured by `deltaTone`
// — "+12%" in green before "from last month".
defineProps({
  label: { type: String, required: true },
  value: { type: String, required: true },
  icon: { type: String, default: '' },
  sub: { type: String, default: '' },
  delta: { type: String, default: '' },
  deltaTone: { type: String, default: '' },
  tone: { type: String, default: '' },
  wide: Boolean,
  accent: Boolean,
})
</script>

<template>
  <div :class="['stat-tile', { wide, accent }]">
    <div class="stat-head">
      <span v-if="icon" class="stat-icon"><UiIcon :name="icon" /></span>
      <span class="stat-label">{{ label }}</span>
    </div>
    <span :class="['stat-value', tone]">{{ value }}</span>
    <span v-if="sub || delta" class="stat-sub">
      <span v-if="delta" :class="['stat-delta', deltaTone]">{{ delta }}</span>{{ delta && sub ? ' ' : '' }}{{ sub }}
    </span>
  </div>
</template>
