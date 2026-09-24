<script setup lang="ts">
import { computed } from 'vue'
import type { Item } from '@/domain/item'
import { isOnHand } from '@/domain/lifecycle'
import { formatMoney, formatPercent, formatSigned } from '@/domain/money'
import { formatDate, itemDaysListed } from '@/lib/format'
import type { SortId } from '@/lib/itemFilters'
import ItemThumb from './ItemThumb.vue'
import PlatformBadge from './PlatformBadge.vue'

// Desktop inventory: every item on one line with its numbers in aligned columns.
// The column headers drive the same sort as the page's select.
const props = withDefaults(defineProps<{ items: Item[]; currency: string; sort: SortId; staleAfter?: number }>(), {
  staleAfter: 45,
})
const emit = defineEmits<{ open: [item: Item]; sell: [item: Item]; sort: [sort: SortId] }>()

const rows = computed(() => props.items.map((item) => ({ item, days: itemDaysListed(item) })))

const isStale = (item: Item, days: number | null) => item.status === 'listed' && days !== null && days >= props.staleAfter

function listedSub(item: Item, days: number | null): string {
  if (item.status === 'sold') return `${days ?? '—'}d to sell`
  if (item.status === 'donated') return item.donation?.org || 'donated'
  if (isStale(item, days)) return 'going stale'
  return item.status === 'listed' ? 'listed' : 'in stock'
}

function listedMain(item: Item, days: number | null): string {
  if (item.status === 'sold') return formatDate(item.sale?.date)
  if (item.status === 'donated') return formatDate(item.donation?.date)
  return days === null ? '—' : `${days}d`
}
</script>

<template>
  <div class="table-wrap">
    <table class="data-table">
      <thead>
        <tr>
          <th class="col-thumb"><span class="sr-only">Photo</span></th>
          <th>
            <button :class="['th-sort', { active: sort === 'title' }]" @click="emit('sort', 'title')">
              Item<span class="th-caret" aria-hidden="true">{{ sort === 'title' ? '▾' : '' }}</span>
            </button>
          </th>
          <th>Status</th>
          <th>
            <button :class="['th-sort', { active: sort === 'oldest' }]" @click="emit('sort', 'oldest')">
              Listed<span class="th-caret" aria-hidden="true">{{ sort === 'oldest' ? '▾' : '' }}</span>
            </button>
          </th>
          <th class="num">Cost</th>
          <th class="num">
            <button
              :class="['th-sort', { active: sort === 'priceHigh' || sort === 'priceLow' }]"
              @click="emit('sort', sort === 'priceHigh' ? 'priceLow' : 'priceHigh')"
            >
              Price<span class="th-caret" aria-hidden="true">{{ sort === 'priceHigh' || sort === 'priceLow' ? '▾' : '' }}</span>
            </button>
          </th>
          <th class="num">Result</th>
          <th class="col-action"><span class="sr-only">Actions</span></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="{ item, days } in rows" :key="item.id" tabindex="0" @click="emit('open', item)" @keydown.enter="emit('open', item)">
          <td class="col-thumb"><ItemThumb :thumbnail="item.thumbnail" /></td>
          <td>
            <div class="cell-title">{{ item.title || 'Untitled item' }}</div>
            <div class="cell-sub">{{ [item.brand, item.size, item.category].filter(Boolean).join(' · ') || '—' }}</div>
          </td>
          <td>
            <div class="cell-badges">
              <PlatformBadge v-if="item.status === 'sold'" :platform="item.sale?.platform" />
              <span v-else-if="item.status === 'donated'" class="badge badge-donated">Donated</span>
              <template v-else-if="item.platforms.length">
                <PlatformBadge v-for="p in item.platforms" :key="p" :platform="p" />
              </template>
              <span v-else class="badge badge-inventory">Not listed</span>
            </div>
          </td>
          <td>
            <div class="cell-title">{{ listedMain(item, days) }}</div>
            <div :class="['cell-sub', { neg: isStale(item, days) }]">{{ listedSub(item, days) }}</div>
          </td>
          <td class="num mono">{{ formatMoney(item.cost, currency) }}</td>
          <td class="num mono">
            {{ item.status === 'sold' && item.sale ? formatMoney(item.sale.price, currency) : item.listPrice ? formatMoney(item.listPrice, currency) : '—' }}
          </td>
          <td class="num">
            <template v-if="item.status === 'sold' && item.profit">
              <div :class="['cell-title', 'mono', item.profit.net >= 0 ? 'pos' : 'neg']">{{ formatSigned(item.profit.net, currency) }}</div>
              <div class="cell-sub">{{ item.profit.feesEstimated ? 'est. · ' : '' }}{{ formatPercent(item.profit.margin) }} margin</div>
            </template>
            <template v-else-if="item.status === 'donated'">
              <div class="cell-title mono neg">-{{ formatMoney(item.cost, currency) }}</div>
              <div class="cell-sub">written off</div>
            </template>
            <template v-else-if="item.projectedNet !== null">
              <div class="cell-title mono">{{ formatMoney(item.projectedNet, currency) }}</div>
              <div class="cell-sub">est. net</div>
            </template>
            <span v-else class="dimmed">—</span>
          </td>
          <td class="col-action">
            <button
              v-if="isOnHand(item.status)"
              class="item-row-sell"
              :aria-label="`Mark ${item.title || 'item'} as sold`"
              @click.stop="emit('sell', item)"
            >
              Sold
            </button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
