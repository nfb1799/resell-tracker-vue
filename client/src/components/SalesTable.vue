<script setup lang="ts">
import type { Item } from '@/domain/item'
import { formatMoney, formatSigned } from '@/domain/money'
import { platformLabel } from '@/domain/platforms'
import { formatDate } from '@/lib/format'

// The desktop sales ledger: every figure behind one month's profit side by side,
// which is what makes "where did the money go" answerable at a glance.
defineProps<{ items: Item[]; currency: string }>()
const emit = defineEmits<{ open: [item: Item] }>()
</script>

<template>
  <div class="table-wrap">
    <table class="data-table">
      <thead>
        <tr>
          <th>Sold</th>
          <th>Item</th>
          <th>Platform</th>
          <th class="num">Listed for</th>
          <th class="num">Accepted</th>
          <th class="num">Payout</th>
          <th class="num">Fees</th>
          <th class="num">Costs</th>
          <th class="num">Net</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="item in items" :key="item.id" tabindex="0" @click="emit('open', item)" @keydown.enter="emit('open', item)">
          <template v-if="item.sale && item.profit">
            <td class="mono">{{ formatDate(item.sale.date) }}</td>
            <td>
              <div class="cell-title">{{ item.title || 'Untitled item' }}</div>
              <div class="cell-sub">{{ [item.brand, item.size].filter(Boolean).join(' · ') || '—' }}</div>
            </td>
            <td>{{ platformLabel(item.sale.platform) }}</td>
            <td class="num mono dimmed">{{ item.sale.listedFor ? formatMoney(item.sale.listedFor, currency) : '—' }}</td>
            <td class="num mono">{{ formatMoney(item.sale.price, currency) }}</td>
            <td class="num mono">{{ formatMoney(item.profit.payout, currency) }}</td>
            <td class="num mono">
              -{{ formatMoney(item.profit.fees, currency) }}<span v-if="item.profit.feesEstimated" class="dimmed"> est.</span>
            </td>
            <td class="num mono">-{{ formatMoney(item.profit.costs, currency) }}</td>
            <td :class="['num', 'mono', 'net', item.profit.net >= 0 ? 'pos' : 'neg']">{{ formatSigned(item.profit.net, currency) }}</td>
          </template>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.net { font-weight: 700; }
</style>
