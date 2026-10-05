<script setup>
import { ref, computed, defineAsyncComponent } from 'vue'
import { useItems } from '../composables/useItems'
import { totalProfit, writeOffTotal, formatMoney, formatPercent, num } from '../lib/money'
import { currentMonthKey, monthKey, monthLabel, daysListed } from '../lib/date'
import { isOnHand, isSold, isDonated } from '../lib/status'
import { PLATFORM_IDS, platformLabel } from '../lib/platforms'
import { lastMonthKeys, rangeKeys, cumulativeNet, tickLabel, relativeChange } from '../lib/series'
import StatTile from './ui/StatTile.vue'
import ItemRow from './ui/ItemRow.vue'
import EmptyState from './ui/EmptyState.vue'
import PanelCard from './ui/PanelCard.vue'
import RangeSelect from './ui/RangeSelect.vue'

// Chart.js is a big dependency; the charts load in their own chunk.
const AreaChart = defineAsyncComponent(() => import('./charts/AreaChart.vue'))
const BarChart = defineAsyncComponent(() => import('./charts/BarChart.vue'))

const STALE_DAYS = 45

const emit = defineEmits(['open', 'sell', 'add', 'settings'])
const { items, loading, currency, feeSettings, settings } = useItems()

const sold = computed(() => items.value.filter(isSold))

const stats = computed(() => {
  const onHand = items.value.filter(isOnHand)
  const [lastKey, thisKey] = lastMonthKeys(2)
  const inMonth = (key) => sold.value.filter(i => monthKey(i.sale?.date) === key)

  const month = totalProfit(inMonth(thisKey), feeSettings.value)
  const lastMonth = totalProfit(inMonth(lastKey), feeSettings.value)
  const allTime = totalProfit(sold.value, feeSettings.value)
  const writeOffs = writeOffTotal(items.value.filter(isDonated))

  const tiedUp = onHand.reduce((sum, i) => sum + num(i.cost), 0)
  const listed = onHand.filter(i => i.status === 'listed')
  const listedValue = listed.reduce((sum, i) => sum + num(i.listPrice), 0)

  const daysToSell = sold.value.map(daysListed).filter(d => d !== null)
  const avgDays = daysToSell.length
    ? Math.round(daysToSell.reduce((a, b) => a + b, 0) / daysToSell.length)
    : null

  return {
    month,
    allTime,
    writeOffs,
    tiedUp,
    onHandCount: onHand.length,
    listedCount: listed.length,
    listedValue,
    avgDays,
    avgMargin: month.gross > 0 ? month.net / month.gross : null,
    netChange: relativeChange(lastMonth.net, month.net),
  }
})

// "+12%" / "-4%", or nothing when last month gives no base to compare with.
const netDelta = computed(() => {
  const c = stats.value.netChange
  if (c === null) return null
  return { text: `${c >= 0 ? '+' : ''}${formatPercent(c)}`, tone: c >= 0 ? 'pos' : 'neg' }
})

// ── profit overview: the running total, month by month ──
const overviewMonths = ref(6)
const overview = computed(() => cumulativeNet(sold.value, feeSettings.value, rangeKeys(sold.value, overviewMonths.value)))
const overviewTooltip = {
  title: (i) => monthLabel(overview.value[i].key),
  label: (i) => `${formatMoney(overview.value[i].total, currency.value)} all-time net`,
}

// ── profit by platform within the chosen range ──
const platformMonths = ref(6)
const byPlatform = computed(() => {
  const keys = new Set(lastMonthKeys(platformMonths.value))
  const inRange = sold.value.filter(i => keys.has(monthKey(i.sale?.date)))
  return PLATFORM_IDS
    .map(id => ({ id, ...totalProfit(inRange.filter(i => i.sale?.platform === id), feeSettings.value) }))
    .filter(r => r.count > 0)
})
const platformTooltip = {
  title: (i) => platformLabel(byPlatform.value[i].id),
  label: (i) => `${formatMoney(byPlatform.value[i].net, currency.value)} net`,
  footer: (i) => {
    const r = byPlatform.value[i]
    return `${r.count} sold · ${formatMoney(r.fees, currency.value)} fees`
  },
}

// ── goal tracker ──
const goal = computed(() => num(settings.value.profitGoal))
const goalShare = computed(() => (goal.value > 0 ? Math.max(0, stats.value.month.net) / goal.value : 0))
const listedShare = computed(() => (stats.value.onHandCount > 0 ? stats.value.listedCount / stats.value.onHandCount : 0))
const pct = (fraction) => `${Math.min(100, Math.round(fraction * 100))}%`

const recentSales = computed(() => [...sold.value]
  .sort((a, b) => (b.sale?.date || '').localeCompare(a.sale?.date || ''))
  .slice(0, 5))

const stale = computed(() => items.value
  .filter(i => i.status === 'listed' && (daysListed(i) ?? 0) >= STALE_DAYS)
  .sort((a, b) => (daysListed(b) ?? 0) - (daysListed(a) ?? 0))
  .slice(0, 5))
</script>

<template>
  <p v-if="loading" class="muted">Loading inventory…</p>

  <EmptyState v-else-if="items.length === 0" title="Nothing tracked yet">
    Add something you have bought to resell. Once you log what it sold for, the
    profit after fees and shipping shows up here.
    <template #action>
      <button class="btn btn-primary" @click="emit('add')">Add your first item</button>
    </template>
  </EmptyState>

  <template v-else>
    <div class="stat-grid">
      <StatTile
        accent
        icon="trend"
        :label="`Net profit · ${monthLabel(currentMonthKey())}`"
        :value="formatMoney(stats.month.net, currency)"
        :delta="netDelta?.text"
        :delta-tone="netDelta?.tone"
        :sub="netDelta ? 'from last month' : `${stats.month.count} sold this month`"
      />
      <StatTile
        icon="percent"
        label="Margin this month"
        :value="formatPercent(stats.avgMargin)"
        :sub="`${formatMoney(stats.month.fees, currency)} in fees`"
      />
      <StatTile
        icon="wallet"
        label="Cash tied up"
        :value="formatMoney(stats.tiedUp, currency)"
        :sub="`${stats.onHandCount} items on hand`"
      />
      <StatTile
        icon="tag"
        label="Listed value"
        :value="formatMoney(stats.listedValue, currency)"
        :sub="`${stats.listedCount} live listings`"
      />
      <StatTile
        wide
        icon="box"
        label="All-time net profit"
        :value="formatMoney(stats.allTime.net, currency)"
        :tone="stats.allTime.net >= 0 ? '' : 'neg'"
        :sub="`${stats.allTime.count} sales · ${formatMoney(stats.allTime.gross, currency)} revenue · ${formatMoney(stats.allTime.fees, currency)} fees`"
      />
      <StatTile
        :wide="stats.writeOffs.count === 0"
        icon="clock"
        label="Avg days to sell"
        :value="stats.avgDays === null ? '—' : `${stats.avgDays} days`"
        sub="listed to sold, across all sales"
      />
      <StatTile
        v-if="stats.writeOffs.count > 0"
        icon="gift"
        label="Written off"
        :value="`-${formatMoney(stats.writeOffs.cost, currency)}`"
        tone="neg"
        :sub="`${stats.writeOffs.count} donated · kept out of sale profit`"
      />
    </div>

    <PanelCard title="Profit overview" sub="All-time net profit at the end of each month">
      <template #actions><RangeSelect v-model="overviewMonths" label="Profit overview range" /></template>
      <AreaChart
        :labels="overview.map(d => tickLabel(d.key))"
        :values="overview.map(d => d.total)"
        :tooltip="overviewTooltip"
        aria-label="All-time net profit by month"
      />
    </PanelCard>

    <PanelCard title="Profit by platform" sub="Net after fees, shipping and cost of goods">
      <template #actions><RangeSelect v-model="platformMonths" label="Profit by platform range" /></template>
      <BarChart
        v-if="byPlatform.length"
        :labels="byPlatform.map(r => platformLabel(r.id))"
        :values="byPlatform.map(r => r.net)"
        :color-tokens="byPlatform.map(r => `${r.id}-color`)"
        :tooltip="platformTooltip"
        aria-label="Net profit by platform"
      />
      <p v-else class="muted chart-box" style="margin: 0; display: grid; place-items: center; font-size: 13px">
        No sales in the last {{ platformMonths }} months.
      </p>
    </PanelCard>

    <!-- Goals and stale stock share a column, so the taller sales list beside
         them does not leave a hole under the short progress card. -->
    <div class="dash-col">
      <PanelCard title="Progress tracker" sub="This month against your goal, and how much stock is live">
        <div class="progress-row">
          <div class="progress-meta">
            <span>Monthly profit goal</span>
            <span v-if="goal > 0">
              <strong>{{ formatMoney(stats.month.net, currency) }}</strong> of {{ formatMoney(goal, currency) }}
            </span>
            <button v-else class="btn btn-sm btn-ghost" style="padding: 0 4px" @click="emit('settings')">
              Set a goal
            </button>
          </div>
          <div class="progress-track">
            <div :class="['progress-fill', { done: goalShare >= 1 }]" :style="{ width: pct(goalShare) }" />
          </div>
        </div>
        <div class="progress-row">
          <div class="progress-meta">
            <span>Stock listed</span>
            <span><strong>{{ stats.listedCount }}</strong> of {{ stats.onHandCount }} on hand · {{ pct(listedShare) }}</span>
          </div>
          <div class="progress-track">
            <div class="progress-fill" :style="{ width: pct(listedShare) }" />
          </div>
        </div>
      </PanelCard>

      <PanelCard v-if="stale.length" title="Sitting longest" :sub="`Listed ${STALE_DAYS}+ days — worth a price drop or a relist`">
        <div class="item-list">
          <ItemRow v-for="item in stale" :key="item.id" :item="item" :currency="currency"
            :fee-settings="feeSettings" :stale-after="STALE_DAYS" sellable
            @open="emit('open', $event)" @sell="emit('sell', $event)" />
        </div>
      </PanelCard>
    </div>

    <PanelCard title="Recent sales" sub="The last five, newest first">
      <div v-if="recentSales.length" class="item-list">
        <ItemRow v-for="item in recentSales" :key="item.id" :item="item" :currency="currency"
          :fee-settings="feeSettings" @open="emit('open', $event)" />
      </div>
      <p v-else class="muted" style="margin: 0; font-size: 13px">No sales logged yet.</p>
    </PanelCard>

  </template>
</template>
