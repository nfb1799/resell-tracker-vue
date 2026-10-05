<script setup>
import { computed } from 'vue'
import { useItems } from '../composables/useItems'
import { useIsDesktop } from '../composables/useMediaQuery'
import { totalProfit, formatMoney, formatSigned } from '../lib/money'
import { monthKey, monthLabel, getLocalDateString } from '../lib/date'
import { itemsToCsv, downloadCsv } from '../lib/csv'
import SalesTable from './SalesTable.vue'
import ItemRow from './ui/ItemRow.vue'
import EmptyState from './ui/EmptyState.vue'

const emit = defineEmits(['open'])
const { items, loading, currency, feeSettings } = useItems()
const isDesktop = useIsDesktop()

// Sold items bucketed by the month they sold in, newest month first.
const months = computed(() => {
  const sold = items.value
    .filter(i => i.status === 'sold')
    .sort((a, b) => (b.sale?.date || '').localeCompare(a.sale?.date || ''))

  const groups = new Map()
  for (const item of sold) {
    const key = monthKey(item.sale?.date) || 'unknown'
    if (!groups.has(key)) groups.set(key, [])
    groups.get(key).push(item)
  }
  return [...groups.entries()].map(([key, group]) => ({
    key,
    items: group,
    totals: totalProfit(group, feeSettings.value),
  }))
})

const saleCount = computed(() => months.value.reduce((n, m) => n + m.items.length, 0))

function handleExport() {
  const sold = items.value.filter(i => i.status === 'sold')
  downloadCsv(`sales-${getLocalDateString()}.csv`, itemsToCsv(sold, feeSettings.value))
}
</script>

<template>
  <p v-if="loading" class="muted">Loading sales…</p>

  <EmptyState v-else-if="months.length === 0" title="No sales logged yet">
    When something sells, open it from Inventory and tap “Mark as sold”. The
    profit after fees, shipping and what you paid lands here.
  </EmptyState>

  <template v-else>
    <div class="page-head">
      <span class="section-label">{{ saleCount }} sales</span>
      <button class="btn btn-sm" @click="handleExport">Export CSV</button>
    </div>

    <div v-for="month in months" :key="month.key">
      <div class="month-head">
        <span class="section-label">{{ monthLabel(month.key) }}</span>
        <span :class="['month-total', month.totals.net >= 0 ? 'pos' : 'neg']">
          {{ formatSigned(month.totals.net, currency) }}
          <span class="dimmed"> · {{ formatMoney(month.totals.gross, currency) }} in</span>
        </span>
      </div>
      <SalesTable
        v-if="isDesktop"
        :items="month.items"
        :currency="currency"
        :fee-settings="feeSettings"
        @open="emit('open', $event)"
      />
      <div v-else class="item-list">
        <ItemRow v-for="item in month.items" :key="item.id" :item="item" :currency="currency"
          :fee-settings="feeSettings" @open="emit('open', $event)" />
      </div>
    </div>
  </template>
</template>
