<script setup>
import { ref, computed } from 'vue'
import { useItems } from '../composables/useItems'
import { computeProfit, totalProfit, formatMoney, formatPercent, num } from '../lib/money'
import { monthLabel, daysListed } from '../lib/date'
import { PLATFORM_IDS, platformLabel } from '../lib/platforms'
import { isOnHand } from '../lib/status'
import { rangeKeys, monthlyTotals, tickLabel } from '../lib/series'
import EmptyState from './ui/EmptyState.vue'
import BreakdownBar from './ui/BreakdownBar.vue'
import PanelCard from './ui/PanelCard.vue'
import RangeSelect from './ui/RangeSelect.vue'
// This whole page is already a lazy chunk, so the chart can load with it.
import BarChart from './charts/BarChart.vue'

const AGE_BUCKETS = [
  { label: '0–30 days', min: 0, max: 30 },
  { label: '31–60', min: 31, max: 60 },
  { label: '61–90', min: 61, max: 90 },
  { label: '90+', min: 91, max: Infinity },
]

const { items, loading, currency, feeSettings } = useItems()

const sold = computed(() => items.value.filter(i => i.status === 'sold'))

const months = ref(12)
const series = computed(() => monthlyTotals(sold.value, feeSettings.value, rangeKeys(sold.value, months.value)))
const seriesTooltip = {
  title: (i) => monthLabel(series.value[i].key),
  label: (i) => `${formatMoney(series.value[i].net, currency.value)} net`,
  footer: (i) => {
    const d = series.value[i]
    return `${d.count} sold · ${formatMoney(d.gross, currency.value)} in · ${formatMoney(d.fees, currency.value)} fees`
  },
}

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
    <PanelCard class="chart-card" title="Net profit by month" sub="After fees, shipping and cost of goods">
      <template #actions><RangeSelect v-model="months" label="Net profit range" /></template>
      <BarChart
        :labels="series.map(d => tickLabel(d.key))"
        :values="series.map(d => d.net)"
        :color-tokens="series.map(d => (d.net >= 0 ? 'chart-1' : 'danger-color'))"
        :tooltip="seriesTooltip"
        aria-label="Net profit by month"
      />
    </PanelCard>

    <PanelCard title="Profit by platform" sub="All-time net, and what each one kept in fees">
      <BreakdownBar
        v-for="row in byPlatform.rows"
        :key="row.id"
        :label="row.label"
        :value="row.net"
        :share="Math.abs(row.net) / byPlatform.max"
        :color="`var(--${row.id}-color)`"
        :currency="currency"
        :note="`${row.count} sold · ${formatMoney(row.fees, currency)} fees`"
      />
    </PanelCard>

    <PanelCard title="Cash sitting in unsold stock" sub="Cost of what you hold, by how long it has sat">
      <BreakdownBar
        v-for="(bucket, i) in aging"
        :key="bucket.label"
        :label="bucket.label"
        :value="bucket.cost"
        :share="bucket.cost / maxAgingCost"
        :color="`var(--chart-${[2, 4, 3, 5][i]})`"
        :currency="currency"
        :note="`${bucket.count} item${bucket.count === 1 ? '' : 's'}`"
      />
    </PanelCard>

    <PanelCard title="By category" sub="Net profit · margin · average days to sell">
      <div class="breakdown">
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
    </PanelCard>

    <PanelCard v-if="best" title="Best flip so far" sub="Highest net profit on a single item">
      <div class="breakdown">
        <div class="breakdown-row">
          <span class="breakdown-label" style="color: var(--text-primary); font-weight: 600">{{ best.item.title }}</span>
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
    </PanelCard>
  </template>
</template>
