<script setup lang="ts">
import { computed, watch } from 'vue'
import EmptyState from '@/components/EmptyState.vue'
import ItemRow from '@/components/ItemRow.vue'
import StatTile from '@/components/StatTile.vue'
import { currentMonthLabel, itemDaysListed } from '@/lib/format'
import { formatMoney, formatPercent, fromDollars, type Cents } from '@/domain/money'
import { useItemsStore } from '@/stores/items'
import { useSettingsStore } from '@/stores/settings'
import { useSheetStore } from '@/stores/sheet'
import { useStatsStore } from '@/stores/stats'
import { useToastStore } from '@/stores/toast'

const STALE_DAYS = 45

const items = useItemsStore()
const settings = useSettingsStore()
const stats = useStatsStore()
const sheet = useSheetStore()
const toast = useToastStore()

// The tiles are the server's figures; refetch whenever an item changes.
watch(
  () => stats.revision,
  () => stats.loadDashboard().catch(() => toast.show('Could not load the overview', 'error')),
  { immediate: true },
)

const currency = computed(() => settings.currency)
const money = (dollars: number) => formatMoney(fromDollars(dollars), currency.value)

const recentSales = computed(() =>
  items.items
    .filter((i) => i.status === 'sold')
    .sort((a, b) => (b.sale?.date ?? '').localeCompare(a.sale?.date ?? ''))
    .slice(0, 5),
)

const stale = computed(() =>
  items.items
    .map((item) => ({ item, days: itemDaysListed(item) ?? 0 }))
    .filter(({ item, days }) => item.status === 'listed' && days >= STALE_DAYS)
    .sort((a, b) => b.days - a.days)
    .slice(0, 5)
    .map(({ item }) => item),
)

const goal = computed<Cents>(() => settings.profitGoal)

const monthSub = computed(() => {
  const d = stats.dashboard
  if (!d) return ''
  return goal.value > 0
    ? `${formatPercent(fromDollars(d.month.net) / goal.value)} of your ${formatMoney(goal.value, currency.value)} goal · ${d.month.count} sold`
    : `${d.month.count} sold · ${money(d.month.gross)} revenue`
})
</script>

<template>
  <p v-if="!items.loaded" class="muted">Loading inventory…</p>

  <EmptyState v-else-if="items.items.length === 0" title="Nothing tracked yet">
    Add something you have bought to resell. Once you log what it sold for, the profit after fees and shipping shows up
    here.
    <template #action>
      <button class="btn btn-primary" @click="sheet.open('new')">Add your first item</button>
    </template>
  </EmptyState>

  <template v-else>
    <div v-if="stats.dashboard" class="stat-grid">
      <StatTile
        wide
        accent
        :label="`Net profit · ${currentMonthLabel()}`"
        :value="money(stats.dashboard.month.net)"
        :tone="stats.dashboard.month.net >= 0 ? 'pos' : 'neg'"
        :sub="monthSub"
      />
      <StatTile label="Margin this month" :value="formatPercent(stats.dashboard.monthMargin)" :sub="`${money(stats.dashboard.month.fees)} in fees`" />
      <StatTile
        label="Avg days to sell"
        :value="stats.dashboard.avgDaysToSell === null ? '—' : String(stats.dashboard.avgDaysToSell)"
        sub="across all sales"
      />
      <StatTile label="Cash tied up" :value="money(stats.dashboard.tiedUp)" :sub="`${stats.dashboard.onHandCount} items on hand`" />
      <StatTile label="Listed value" :value="money(stats.dashboard.listedValue)" :sub="`${stats.dashboard.listedCount} live listings`" />
      <StatTile
        wide
        label="All-time net profit"
        :value="money(stats.dashboard.allTime.net)"
        :tone="stats.dashboard.allTime.net >= 0 ? 'pos' : 'neg'"
        :sub="`${stats.dashboard.allTime.count} sales · ${money(stats.dashboard.allTime.gross)} revenue · ${money(stats.dashboard.allTime.fees)} fees`"
      />
      <StatTile
        v-if="stats.dashboard.writeOffs.count > 0"
        wide
        label="Written off"
        :value="`-${money(stats.dashboard.writeOffs.cost)}`"
        tone="neg"
        :sub="`${stats.dashboard.writeOffs.count} donated · not counted against sale profit`"
      />
    </div>

    <div v-if="stale.length > 0">
      <div class="page-head">
        <span class="section-label">Sitting longest</span>
        <span class="dimmed small-note">{{ STALE_DAYS }}+ days listed</span>
      </div>
      <div class="item-list list-gap">
        <ItemRow
          v-for="item in stale"
          :key="item.id"
          :item="item"
          :currency="currency"
          :stale-after="STALE_DAYS"
          can-sell
          @open="sheet.openItem"
          @sell="sheet.open('sell', $event)"
        />
      </div>
    </div>

    <div v-if="recentSales.length > 0">
      <span class="section-label">Recent sales</span>
      <div class="item-list list-gap">
        <ItemRow v-for="item in recentSales" :key="item.id" :item="item" :currency="currency" @open="sheet.openItem" />
      </div>
    </div>
  </template>
</template>

<style scoped>
.small-note { font-size: 12px; }
.list-gap { margin-top: 8px; }
</style>
