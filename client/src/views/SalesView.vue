<script setup lang="ts">
import { computed } from 'vue'
import EmptyState from '@/components/EmptyState.vue'
import ItemRow from '@/components/ItemRow.vue'
import SalesTable from '@/components/SalesTable.vue'
import { useIsDesktop } from '@/composables/useMediaQuery'
import { monthKey } from '@/domain/dates'
import { formatMoney, formatSigned } from '@/domain/money'
import { totalProfit } from '@/domain/profit'
import { monthLabel } from '@/lib/format'
import { useItemsStore } from '@/stores/items'
import { useSettingsStore } from '@/stores/settings'
import { useSheetStore } from '@/stores/sheet'

const items = useItemsStore()
const settings = useSettingsStore()
const sheet = useSheetStore()
const isDesktop = useIsDesktop()

// Sold items bucketed by the month they sold in, newest month first. The totals
// add up the server's per-sale figures, in whole cents.
const months = computed(() => {
  const sold = items.items
    .filter((i) => i.status === 'sold' && i.profit)
    .sort((a, b) => (b.sale?.date ?? '').localeCompare(a.sale?.date ?? ''))

  const groups = new Map<string, typeof sold>()
  for (const item of sold) {
    const key = monthKey(item.sale?.date) || 'unknown'
    groups.set(key, [...(groups.get(key) ?? []), item])
  }
  return [...groups.entries()].map(([key, group]) => ({
    key,
    items: group,
    totals: totalProfit(group.map((i) => i.profit!)),
  }))
})

const saleCount = computed(() => months.value.reduce((n, m) => n + m.items.length, 0))
</script>

<template>
  <p v-if="!items.loaded" class="muted">Loading sales…</p>

  <EmptyState v-else-if="months.length === 0" title="No sales logged yet">
    When something sells, open it from Inventory and tap “Mark as sold”. The profit after fees, shipping and what you paid
    lands here.
  </EmptyState>

  <template v-else>
    <div class="page-head">
      <span class="section-label">{{ saleCount }} sales</span>
    </div>

    <div v-for="month in months" :key="month.key">
      <div class="month-head">
        <span class="section-label">{{ monthLabel(month.key) }}</span>
        <span :class="['month-total', month.totals.net >= 0 ? 'pos' : 'neg']">
          {{ formatSigned(month.totals.net, settings.currency) }}
          <span class="dimmed"> · {{ formatMoney(month.totals.gross, settings.currency) }} in</span>
        </span>
      </div>
      <SalesTable v-if="isDesktop" :items="month.items" :currency="settings.currency" @open="sheet.openItem" />
      <div v-else class="item-list">
        <ItemRow v-for="item in month.items" :key="item.id" :item="item" :currency="settings.currency" @open="sheet.openItem" />
      </div>
    </div>
  </template>
</template>
