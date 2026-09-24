<script setup lang="ts">
import { BarElement, CategoryScale, Chart, LinearScale, Tooltip, type ChartData, type ChartOptions } from 'chart.js'
import { computed, watch } from 'vue'
import { Bar } from 'vue-chartjs'
import BreakdownBar from '@/components/BreakdownBar.vue'
import EmptyState from '@/components/EmptyState.vue'
import { useThemeColors } from '@/composables/useThemeColors'
import { formatMoney, formatPercent, fromDollars } from '@/domain/money'
import { PLATFORM_IDS, platformLabel } from '@/domain/platforms'
import { monthLabel, monthTick } from '@/lib/format'
import { useSettingsStore } from '@/stores/settings'
import { useStatsStore } from '@/stores/stats'
import { useToastStore } from '@/stores/toast'

// Only what a bar chart needs, so the rest of Chart.js is tree-shaken away.
Chart.register(BarElement, CategoryScale, LinearScale, Tooltip)

const stats = useStatsStore()
const settings = useSettingsStore()
const toast = useToastStore()

watch(
  () => stats.revision,
  () => stats.loadTrends().catch(() => toast.show('Could not load trends', 'error')),
  { immediate: true },
)

// Platform colours come from the tokens, so a platform added to the registry
// (with its --<id>-color) shows up here with no change to this file.
const colors = useThemeColors([
  'success-color',
  'danger-color',
  'border-subtle',
  'text-dimmed',
  'text-primary',
  'bg-secondary',
  'border-color',
  'accent-primary',
  ...PLATFORM_IDS.map((id) => `${id}-color`),
] as const)
const color = (name: string) => (colors.value as Record<string, string>)[name] ?? ''

const money = (dollars: number) => formatMoney(fromDollars(dollars), settings.currency)
const trends = computed(() => stats.trends)

const chartData = computed<ChartData<'bar'>>(() => {
  const months = trends.value?.months ?? []
  return {
    labels: months.map((m) => monthTick(m.month)),
    datasets: [
      {
        data: months.map((m) => m.totals.net),
        backgroundColor: months.map((m) => (m.totals.net >= 0 ? color('success-color') : color('danger-color'))),
        borderRadius: 4,
        maxBarThickness: 38,
      },
    ],
  }
})

const chartOptions = computed<ChartOptions<'bar'>>(() => {
  const months = trends.value?.months ?? []
  return {
    responsive: true,
    maintainAspectRatio: false,
    animation: false,
    scales: {
      x: { grid: { display: false }, border: { display: false }, ticks: { color: color('text-dimmed'), font: { size: 10 } } },
      y: {
        grid: { color: color('border-subtle') },
        border: { display: false },
        ticks: {
          color: color('text-dimmed'),
          font: { size: 10 },
          callback: (v) => (Math.abs(Number(v)) >= 1000 ? `${Math.round(Number(v) / 100) / 10}k` : v),
        },
      },
    },
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: color('bg-secondary'),
        borderColor: color('border-color'),
        borderWidth: 1,
        titleColor: color('text-primary'),
        bodyColor: color('text-dimmed'),
        padding: 10,
        displayColors: false,
        callbacks: {
          title: (ctx) => monthLabel(months[ctx[0]?.dataIndex ?? 0]?.month ?? ''),
          label: (ctx) => {
            const t = months[ctx.dataIndex]?.totals
            return t ? [`${money(t.net)} net`, `${t.count} sold · ${money(t.gross)} in · ${money(t.fees)} fees`] : ''
          },
        },
      },
    },
  }
})

// A text version of the chart for screen readers.
const chartSummary = computed(() =>
  (trends.value?.months ?? []).map((m) => `${monthLabel(m.month)}: ${money(m.totals.net)} net`).join('; '),
)

const platformMax = computed(() => Math.max(1, ...(trends.value?.byPlatform ?? []).map((p) => Math.abs(p.totals.net))))
const agingMax = computed(() => Math.max(1, ...(trends.value?.aging ?? []).map((a) => a.cost)))
</script>

<template>
  <p v-if="!trends" class="muted">Loading charts…</p>

  <EmptyState v-else-if="trends.months.length === 0" title="No numbers yet">
    Log a sale or two and this page fills in: profit by month, which platform actually pays, and what sits unsold the
    longest.
  </EmptyState>

  <template v-else>
    <div class="card chart-card">
      <div class="chart-head">
        <span class="section-label">Net profit by month</span>
        <span class="dimmed small-note">after fees &amp; shipping</span>
      </div>
      <div class="chart-box">
        <Bar :data="chartData" :options="chartOptions" role="img" :aria-label="`Net profit by month. ${chartSummary}`" />
      </div>
    </div>

    <div class="card settings-group">
      <span class="section-label">Profit by platform</span>
      <BreakdownBar
        v-for="row in trends.byPlatform"
        :key="row.platform"
        :label="platformLabel(row.platform)"
        :value="money(row.totals.net)"
        :share="Math.abs(row.totals.net) / platformMax"
        :color="color(`${row.platform}-color`)"
        :note="`${row.totals.count} sold · ${money(row.totals.fees)} fees`"
      />
    </div>

    <div class="card settings-group">
      <span class="section-label">Cash sitting in unsold stock</span>
      <BreakdownBar
        v-for="bucket in trends.aging"
        :key="bucket.label"
        :label="bucket.label"
        :value="money(bucket.cost)"
        :share="bucket.cost / agingMax"
        :color="color('accent-primary')"
        :note="`${bucket.count} item${bucket.count === 1 ? '' : 's'}`"
      />
    </div>

    <div class="card">
      <span class="section-label">By category</span>
      <div class="breakdown list-gap">
        <div v-for="row in trends.byCategory" :key="row.category" class="breakdown-row">
          <span class="breakdown-label">{{ row.category }}<span class="dimmed"> · {{ row.totals.count }} sold</span></span>
          <span :class="['breakdown-value', row.totals.net >= 0 ? 'pos' : 'neg']">
            {{ money(row.totals.net) }}
            <span class="dimmed plain">
              {{ formatPercent(row.margin) }}{{ row.avgDaysToSell !== null ? ` · ${row.avgDaysToSell}d` : '' }}
            </span>
          </span>
        </div>
      </div>
    </div>

    <div v-if="trends.best" class="card">
      <span class="section-label">Best flip so far</span>
      <div class="breakdown list-gap">
        <div class="breakdown-row">
          <span class="breakdown-label">{{ trends.best.title }}</span>
          <span class="breakdown-value pos">{{ money(trends.best.net) }}</span>
        </div>
        <div class="breakdown-row">
          <span class="breakdown-label">
            {{ money(trends.best.cogs) }} in · {{ money(trends.best.price) }} out on {{ platformLabel(trends.best.platform) }}
          </span>
          <span class="breakdown-value">{{ trends.best.roi === null ? '—' : `${formatPercent(trends.best.roi)} ROI` }}</span>
        </div>
      </div>
    </div>
  </template>
</template>

<style scoped>
.chart-box { position: relative; height: 220px; }
.small-note { font-size: 12px; }
.list-gap { margin-top: 8px; }
.plain { font-weight: 400; }
</style>
