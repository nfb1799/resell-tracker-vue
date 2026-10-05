<script setup>
import { computed } from 'vue'
import { Bar } from 'vue-chartjs'
import { Chart, BarElement, CategoryScale, LinearScale, Tooltip } from 'chart.js'
import { useItems } from '../composables/useItems'
import { useThemeColors } from '../composables/useThemeColors'
import { computeProfit, totalProfit, formatMoney, formatPercent, num } from '../lib/money'
import { monthKey, monthLabel, daysListed } from '../lib/date'
import { PLATFORM_IDS, platformLabel } from '../lib/platforms'
import { isOnHand } from '../lib/status'
import EmptyState from './ui/EmptyState.vue'
import BreakdownBar from './ui/BreakdownBar.vue'

// Register only the pieces the one bar chart uses, so the rest of Chart.js
// tree-shakes out of this lazy chunk.
Chart.register(BarElement, CategoryScale, LinearScale, Tooltip)

const TOKENS = [
  'success-color', 'danger-color', 'border-subtle', 'text-dimmed', 'bg-secondary',
  'border-color', 'text-primary', 'accent-primary',
  'depop-color', 'ebay-color', 'vinted-color', 'other-color',
]

const MONTHS_SHOWN = 12
const AGE_BUCKETS = [
  { label: '0–30 days', min: 0, max: 30 },
  { label: '31–60', min: 31, max: 60 },
  { label: '61–90', min: 61, max: 90 },
  { label: '90+', min: 91, max: Infinity },
]

// Every calendar month from the first sale to now, so gaps read as gaps rather
// than being silently skipped by the axis.
function monthSeries(sold, feeSettings) {
  if (sold.length === 0) return []
  const keys = sold.map(i => monthKey(i.sale?.date)).filter(Boolean).sort()
  if (keys.length === 0) return []
  const [startY, startM] = keys[0].split('-').map(Number)
  const now = new Date()

  const series = []
  const cursor = new Date(startY, startM - 1, 1)
  while (cursor <= now) {
    const key = `${cursor.getFullYear()}-${String(cursor.getMonth() + 1).padStart(2, '0')}`
    const inMonth = sold.filter(i => monthKey(i.sale?.date) === key)
    const totals = totalProfit(inMonth, feeSettings)
    series.push({
      key,
      // Axis ticks stay short; January carries the year so the span is readable.
      label: cursor.toLocaleDateString(undefined, {
        month: 'short',
        ...(cursor.getMonth() === 0 ? { year: '2-digit' } : {}),
      }),
      net: totals.net,
      gross: totals.gross,
      fees: totals.fees,
      count: totals.count,
    })
    cursor.setMonth(cursor.getMonth() + 1)
  }
  return series.slice(-MONTHS_SHOWN)
}

const { items, loading, currency, feeSettings } = useItems()
const colors = useThemeColors(TOKENS)

const sold = computed(() => items.value.filter(i => i.status === 'sold'))
const series = computed(() => monthSeries(sold.value, feeSettings.value))

const chartData = computed(() => ({
  labels: series.value.map(d => d.label),
  datasets: [{
    data: series.value.map(d => d.net),
    backgroundColor: series.value.map(d => (d.net >= 0 ? colors.value['success-color'] : colors.value['danger-color'])),
    borderRadius: { topLeft: 4, topRight: 4 },
    borderSkipped: false,
    maxBarThickness: 38,
  }],
}))

const chartOptions = computed(() => {
  const c = colors.value
  const tick = { color: c['text-dimmed'], font: { size: 10 } }
  return {
    responsive: true,
    maintainAspectRatio: false,
    animation: false,
    layout: { padding: { top: 4, right: 12 } },
    scales: {
      x: { grid: { display: false }, border: { display: false }, ticks: { ...tick, autoSkip: true, maxRotation: 0 } },
      y: {
        grid: { color: c['border-subtle'] },
        border: { display: false },
        ticks: {
          ...tick,
          callback: (v) => (Math.abs(v) >= 1000 ? `${Math.round(v / 100) / 10}k` : v),
        },
      },
    },
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: c['bg-secondary'],
        borderColor: c['border-color'],
        borderWidth: 1,
        cornerRadius: 10,
        padding: { x: 11, y: 8 },
        titleColor: c['text-primary'],
        bodyColor: c['text-primary'],
        footerColor: c['text-dimmed'],
        footerFont: { weight: 'normal', size: 12 },
        titleFont: { size: 12.5 },
        bodyFont: { size: 12.5 },
        displayColors: false,
        callbacks: {
          title: (ctx) => monthLabel(series.value[ctx[0].dataIndex].key),
          label: (ctx) => `${formatMoney(series.value[ctx.dataIndex].net, currency.value)} net`,
          footer: (ctx) => {
            const d = series.value[ctx[0].dataIndex]
            return `${d.count} sold · ${formatMoney(d.gross, currency.value)} in · ${formatMoney(d.fees, currency.value)} fees`
          },
        },
      },
    },
  }
})

const byPlatform = computed(() => {
  const rows = PLATFORM_IDS.map(id => {
    const group = sold.value.filter(i => i.sale?.platform === id)
    return { id, label: platformLabel(id), ...totalProfit(group, feeSettings.value) }
  }).filter(r => r.count > 0)
  const max = Math.max(1, ...rows.map(r => Math.abs(r.net)))
  return { rows: rows.sort((a, b) => b.net - a.net), max }
})

const byCategory = computed(() => {
  const groups = new Map()
  for (const item of sold.value) {
    const key = item.category?.trim() || 'Uncategorised'
    if (!groups.has(key)) groups.set(key, [])
    groups.get(key).push(item)
  }
  return [...groups.entries()]
    .map(([label, group]) => {
      const t = totalProfit(group, feeSettings.value)
      const days = group.map(daysListed).filter(d => d !== null)
      return {
        label,
        ...t,
        margin: t.gross > 0 ? t.net / t.gross : null,
        avgDays: days.length ? Math.round(days.reduce((a, b) => a + b, 0) / days.length) : null,
      }
    })
    .sort((a, b) => b.net - a.net)
    .slice(0, 8)
})

const aging = computed(() => {
  const onHand = items.value.filter(isOnHand)
  return AGE_BUCKETS.map(bucket => {
    const group = onHand.filter(i => {
      const d = daysListed(i)
      return d !== null && d >= bucket.min && d <= bucket.max
    })
    return {
      label: bucket.label,
      count: group.length,
      cost: group.reduce((sum, i) => sum + num(i.cost), 0),
    }
  })
})
const maxAgingCost = computed(() => Math.max(1, ...aging.value.map(a => a.cost)))

const best = computed(() => {
  if (sold.value.length === 0) return null
  return sold.value
    .map(item => ({ item, profit: computeProfit(item, feeSettings.value) }))
    .sort((a, b) => b.profit.net - a.profit.net)[0]
})
</script>

<template>
  <p v-if="loading" class="muted">Loading…</p>

  <EmptyState v-else-if="sold.length === 0" title="No numbers yet">
    Log a sale or two and this page fills in: profit by month, which platform
    actually pays, and what sits unsold the longest.
  </EmptyState>

  <template v-else>
    <div class="card chart-card">
      <div class="chart-head">
        <span class="section-label">Net profit by month</span>
        <span class="dimmed" style="font-size: 12px">after fees &amp; shipping</span>
      </div>
      <div style="position: relative; height: 220px">
        <Bar :data="chartData" :options="chartOptions" aria-label="Net profit by month" />
      </div>
    </div>

    <div class="card settings-group">
      <span class="section-label">Profit by platform</span>
      <BreakdownBar
        v-for="row in byPlatform.rows"
        :key="row.id"
        :label="row.label"
        :value="row.net"
        :share="Math.abs(row.net) / byPlatform.max"
        :color="colors[`${row.id}-color`]"
        :currency="currency"
        :note="`${row.count} sold · ${formatMoney(row.fees, currency)} fees`"
      />
    </div>

    <div class="card settings-group">
      <span class="section-label">Cash sitting in unsold stock</span>
      <BreakdownBar
        v-for="bucket in aging"
        :key="bucket.label"
        :label="bucket.label"
        :value="bucket.cost"
        :share="bucket.cost / maxAgingCost"
        :color="colors['accent-primary']"
        :currency="currency"
        :note="`${bucket.count} item${bucket.count === 1 ? '' : 's'}`"
      />
    </div>

    <div class="card">
      <span class="section-label">By category</span>
      <div class="breakdown" style="margin-top: 8px">
        <div v-for="row in byCategory" :key="row.label" class="breakdown-row">
          <span class="breakdown-label">
            {{ row.label }}<span class="dimmed"> · {{ row.count }} sold</span>
          </span>
          <span :class="['breakdown-value', row.net >= 0 ? 'pos' : 'neg']">
            {{ formatMoney(row.net, currency) }}
            <span class="dimmed" style="font-weight: 400">
              {{ formatPercent(row.margin) }}{{ row.avgDays !== null ? ` · ${row.avgDays}d` : '' }}
            </span>
          </span>
        </div>
      </div>
    </div>

    <div v-if="best" class="card">
      <span class="section-label">Best flip so far</span>
      <div class="breakdown" style="margin-top: 8px">
        <div class="breakdown-row">
          <span class="breakdown-label">{{ best.item.title }}</span>
          <span class="breakdown-value pos">{{ formatMoney(best.profit.net, currency) }}</span>
        </div>
        <div class="breakdown-row">
          <span class="breakdown-label">
            {{ formatMoney(best.profit.cogs, currency) }} in ·
            {{ formatMoney(best.item.sale?.price, currency) }} out on
            {{ platformLabel(best.item.sale?.platform) }}
          </span>
          <span class="breakdown-value">
            {{ best.profit.roi === null ? '—' : `${formatPercent(best.profit.roi)} ROI` }}
          </span>
        </div>
      </div>
    </div>
  </template>
</template>
