<script setup>
import { computed } from 'vue'
import { useItems } from '../composables/useItems'
import { totalProfit, writeOffTotal, formatMoney, formatPercent, num } from '../lib/money'
import { currentMonthKey, monthKey, monthLabel, daysListed } from '../lib/date'
import { isOnHand, isSold, isDonated } from '../lib/status'
import StatTile from './ui/StatTile.vue'
import ItemRow from './ui/ItemRow.vue'
import EmptyState from './ui/EmptyState.vue'

const STALE_DAYS = 45

const emit = defineEmits(['open', 'sell', 'add'])
const { items, loading, currency, feeSettings, settings } = useItems()

const stats = computed(() => {
  const sold = items.value.filter(isSold)
  const onHand = items.value.filter(isOnHand)
  const thisMonth = sold.filter(i => monthKey(i.sale?.date) === currentMonthKey())

  const month = totalProfit(thisMonth, feeSettings.value)
  const allTime = totalProfit(sold, feeSettings.value)
  const writeOffs = writeOffTotal(items.value.filter(isDonated))

  const tiedUp = onHand.reduce((sum, i) => sum + num(i.cost), 0)
  const listed = onHand.filter(i => i.status === 'listed')
  const listedValue = listed.reduce((sum, i) => sum + num(i.listPrice), 0)

  const daysToSell = sold.map(daysListed).filter(d => d !== null)
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
  }
})

const recentSales = computed(() => items.value
  .filter(i => i.status === 'sold')
  .sort((a, b) => (b.sale?.date || '').localeCompare(a.sale?.date || ''))
  .slice(0, 5))

const stale = computed(() => items.value
  .filter(i => i.status === 'listed' && (daysListed(i) ?? 0) >= STALE_DAYS)
  .sort((a, b) => (daysListed(b) ?? 0) - (daysListed(a) ?? 0))
  .slice(0, 5))

const goal = computed(() => num(settings.value.profitGoal))
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
        wide
        accent
        :label="`Net profit · ${monthLabel(currentMonthKey())}`"
        :value="formatMoney(stats.month.net, currency)"
        :tone="stats.month.net >= 0 ? 'pos' : 'neg'"
        :sub="goal > 0
          ? `${formatPercent(stats.month.net / goal)} of your ${formatMoney(goal, currency)} goal · ${stats.month.count} sold`
          : `${stats.month.count} sold · ${formatMoney(stats.month.gross, currency)} revenue`"
      />
      <StatTile
        label="Margin this month"
        :value="formatPercent(stats.avgMargin)"
        :sub="`${formatMoney(stats.month.fees, currency)} in fees`"
      />
      <StatTile
        label="Avg days to sell"
        :value="stats.avgDays === null ? '—' : `${stats.avgDays}`"
        sub="across all sales"
      />
      <StatTile
        label="Cash tied up"
        :value="formatMoney(stats.tiedUp, currency)"
        :sub="`${stats.onHandCount} items on hand`"
      />
      <StatTile
        label="Listed value"
        :value="formatMoney(stats.listedValue, currency)"
        :sub="`${stats.listedCount} live listings`"
      />
      <StatTile
        wide
        label="All-time net profit"
        :value="formatMoney(stats.allTime.net, currency)"
        :tone="stats.allTime.net >= 0 ? 'pos' : 'neg'"
        :sub="`${stats.allTime.count} sales · ${formatMoney(stats.allTime.gross, currency)} revenue · ${formatMoney(stats.allTime.fees, currency)} fees`"
      />
      <StatTile
        v-if="stats.writeOffs.count > 0"
        wide
        label="Written off"
        :value="`-${formatMoney(stats.writeOffs.cost, currency)}`"
        tone="neg"
        :sub="`${stats.writeOffs.count} donated · not counted against sale profit`"
      />
    </div>

    <div v-if="stale.length > 0">
      <div class="page-head">
        <span class="section-label">Sitting longest</span>
        <span class="dimmed" style="font-size: 12px">{{ STALE_DAYS }}+ days listed</span>
      </div>
      <div class="item-list" style="margin-top: 8px">
        <ItemRow v-for="item in stale" :key="item.id" :item="item" :currency="currency"
          :fee-settings="feeSettings" :stale-after="STALE_DAYS" sellable
          @open="emit('open', $event)" @sell="emit('sell', $event)" />
      </div>
    </div>

    <div v-if="recentSales.length > 0">
      <span class="section-label">Recent sales</span>
      <div class="item-list" style="margin-top: 8px">
        <ItemRow v-for="item in recentSales" :key="item.id" :item="item" :currency="currency"
          :fee-settings="feeSettings" @open="emit('open', $event)" />
      </div>
    </div>
  </template>
</template>
