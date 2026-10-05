<script setup>
import { computed } from 'vue'
import { computeProfit, formatMoney, formatSigned } from '../lib/money'
import { formatDate } from '../lib/date'
import { platformLabel } from '../lib/platforms'

// The sales ledger: every figure behind one month's profit, side by side, which
// is the view that makes "where did the money go" answerable at a glance.
const props = defineProps({
  items: { type: Array, required: true },
  currency: { type: String, required: true },
  feeSettings: { type: Object, required: true },
})
const emit = defineEmits(['open'])

const rows = computed(() => props.items.map(item => ({
  item,
  p: computeProfit(item, props.feeSettings),
})))
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
        <tr v-for="{ item, p } in rows" :key="item.id" tabindex="0"
          @click="emit('open', item)" @keydown.enter="emit('open', item)">
          <td class="mono">{{ formatDate(item.sale?.date) }}</td>
          <td>
            <div class="cell-title">{{ item.title || 'Untitled item' }}</div>
            <div class="cell-sub">{{ [item.brand, item.size].filter(Boolean).join(' · ') || '—' }}</div>
          </td>
          <td>{{ platformLabel(item.sale?.platform) }}</td>
          <td class="num mono dimmed">
            {{ item.sale?.listedFor ? formatMoney(item.sale.listedFor, currency) : '—' }}
          </td>
          <td class="num mono">{{ formatMoney(item.sale?.price, currency) }}</td>
          <td class="num mono">{{ formatMoney(p.payout, currency) }}</td>
          <td class="num mono">
            -{{ formatMoney(p.fees, currency) }}<span v-if="p.feesEstimated" class="dimmed"> est.</span>
          </td>
          <td class="num mono">-{{ formatMoney(p.costs, currency) }}</td>
          <td :class="['num', 'mono', p.net >= 0 ? 'pos' : 'neg']" style="font-weight: 700">
            {{ formatSigned(p.net, currency) }}
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
