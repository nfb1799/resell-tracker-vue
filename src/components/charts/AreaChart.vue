<script setup>
import { computed } from 'vue'
import { Line } from 'vue-chartjs'
import { CHART_TOKENS, baseOptions } from './chartTheme'
import { useThemeColors } from '../../composables/useThemeColors'

// A smooth line over a fill that fades from the accent to nothing.
const props = defineProps({
  labels: { type: Array, required: true },
  values: { type: Array, required: true },
  // { title(i), label(i), footer(i) } → strings, by data index.
  tooltip: { type: Object, required: true },
  ariaLabel: { type: String, required: true },
})

const colors = useThemeColors(CHART_TOKENS)

// Canvas gradients need the plotted area, which only exists once Chart.js has
// laid the chart out — hence a scriptable colour rather than a fixed one.
const fill = (rgb) => (ctx) => {
  const { chart } = ctx
  const area = chart.chartArea
  if (!area) return 'transparent'
  const g = chart.ctx.createLinearGradient(0, area.top, 0, area.bottom)
  g.addColorStop(0, `rgba(${rgb}, .45)`)
  g.addColorStop(1, `rgba(${rgb}, 0)`)
  return g
}

const data = computed(() => ({
  labels: props.labels,
  datasets: [{
    data: props.values,
    borderColor: colors.value['accent-primary'],
    borderWidth: 2,
    backgroundColor: fill(colors.value['accent-rgb']),
    fill: 'origin',
    tension: 0.42,
    pointRadius: 0,
    pointHoverRadius: 5,
    pointHoverBackgroundColor: colors.value['accent-primary'],
    pointHoverBorderColor: colors.value['bg-secondary'],
    pointHoverBorderWidth: 2,
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
    <Line :data="data" :options="options" :aria-label="ariaLabel" role="img" />
  </div>
</template>
