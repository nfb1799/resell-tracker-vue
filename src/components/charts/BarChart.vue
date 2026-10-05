<script setup>
import { computed } from 'vue'
import { Bar } from 'vue-chartjs'
import { CHART_TOKENS, baseOptions } from './chartTheme'
import { useThemeColors } from '../../composables/useThemeColors'

// Rounded bars, each in its own colour. `colorTokens` names a token per bar
// (one of CHART_TOKENS), so the caller decides what the colour means — a
// platform, or gain against loss.
const props = defineProps({
  labels: { type: Array, required: true },
  values: { type: Array, required: true },
  colorTokens: { type: Array, required: true },
  // { title(i), label(i), footer(i) } → strings, by data index.
  tooltip: { type: Object, required: true },
  ariaLabel: { type: String, required: true },
})

const colors = useThemeColors(CHART_TOKENS)

const resolved = computed(() => props.colorTokens.map(t => colors.value[t]))

const data = computed(() => ({
  labels: props.labels,
  datasets: [{
    data: props.values,
    backgroundColor: resolved.value,
    hoverBackgroundColor: resolved.value,
    borderRadius: 6,
    borderSkipped: false,
    maxBarThickness: 44,
    categoryPercentage: 0.72,
  }],
}))

const options = computed(() => baseOptions(colors.value, {
  title: (items) => props.tooltip.title(items[0].dataIndex),
  label: (item) => props.tooltip.label(item.dataIndex),
  footer: (items) => props.tooltip.footer?.(items[0].dataIndex) ?? '',
}))
</script>

<template>
  <div class="chart-box">
    <Bar :data="data" :options="options" :aria-label="ariaLabel" role="img" />
  </div>
</template>
