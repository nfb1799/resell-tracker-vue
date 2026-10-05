<script setup>
// The "Last 6 months" picker that sits in a panel's header.
const RANGES = [3, 6, 12]

defineProps({ label: { type: String, default: 'Time range' } })
const months = defineModel({ type: Number, default: 6 })
</script>

<template>
  <select v-model.number="months" class="range-select" :aria-label="label">
    <option v-for="n in RANGES" :key="n" :value="n">Last {{ n }} months</option>
  </select>
</template>
