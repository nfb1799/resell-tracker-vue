<script setup>
import { computed } from 'vue'
import { computeProfit, projectedNet, formatMoney, formatSigned, formatPercent } from '../lib/money'
import { formatDate, daysListed } from '../lib/date'
import { isSold, isDonated, isOnHand } from '../lib/status'
import ItemThumb from './ui/ItemThumb.vue'
import ItemStatusBadges from './ui/ItemStatusBadges.vue'
import SortHeader from './ui/SortHeader.vue'

// Desktop-only list view. A phone row stacks a few facts vertically because
// that is all that fits; a desktop has room to put every item on one line with
// its numbers in aligned columns, which is what makes a list scannable.
//
// The column headers drive the sort the page already owns, so clicking a header
// and picking from the select stay the same state.
const props = defineProps({
  items: { type: Array, required: true },
  currency: { type: String, required: true },
  feeSettings: { type: Object, required: true },
  sort: { type: String, required: true },
  staleAfter: { type: Number, default: 45 },
})
const emit = defineEmits(['sort', 'open', 'sell'])

const rows = computed(() => props.items.map(item => {
  const sold = isSold(item)
  const onHand = isOnHand(item)
  const days = daysListed(item)
  return {
    item,
    sold,
    donated: isDonated(item),
    onHand,
    profit: sold ? computeProfit(item, props.feeSettings) : null,
    days,
    stale: item.status === 'listed' && days !== null && days >= props.staleAfter,
    projected: onHand ? projectedNet(item, props.feeSettings) : null,
  }
}))
</script>

<template>
  <div class="table-wrap">
    <table class="data-table">
      <thead>
        <tr>
          <th class="col-thumb"><span class="sr-only">Photo</span></th>
          <SortHeader label="Item" :active="sort === 'title'" @click="emit('sort', 'title')" />
          <th>Status</th>
          <SortHeader label="Listed" :active="sort === 'oldest'" @click="emit('sort', 'oldest')" />
          <th class="num">Cost</th>
          <SortHeader
            label="Price"
            align="right"
            :active="sort === 'priceHigh' || sort === 'priceLow'"
            @click="emit('sort', sort === 'priceHigh' ? 'priceLow' : 'priceHigh')"
          />
          <th class="num">Result</th>
          <th class="col-action"><span class="sr-only">Actions</span></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="r in rows" :key="r.item.id" tabindex="0"
          @click="emit('open', r.item)" @keydown.enter="emit('open', r.item)">
          <td class="col-thumb"><ItemThumb :item="r.item" /></td>

          <td>
            <div class="cell-title">{{ r.item.title || 'Untitled item' }}</div>
            <div class="cell-sub">
              {{ [r.item.brand, r.item.size, r.item.category].filter(Boolean).join(' · ') || '—' }}
            </div>
          </td>

          <td>
            <div class="cell-badges"><ItemStatusBadges :item="r.item" /></div>
          </td>

          <td>
            <div class="cell-title">
              {{ r.sold ? formatDate(r.item.sale?.date)
                : r.donated ? formatDate(r.item.donation?.date)
                  : r.days === null ? '—' : `${r.days}d` }}
            </div>
            <div :class="['cell-sub', { neg: r.stale }]">
              {{ r.sold ? `${r.days ?? '—'}d to sell`
                : r.donated ? (r.item.donation?.org || 'donated')
                  : r.stale ? 'going stale'
                    : r.item.status === 'listed' ? 'listed'
                      : 'in stock' }}
            </div>
          </td>

          <td class="num mono">{{ formatMoney(r.item.cost, currency) }}</td>

          <td class="num mono">
            {{ r.sold ? formatMoney(r.item.sale?.price, currency)
              : r.item.listPrice ? formatMoney(r.item.listPrice, currency) : '—' }}
          </td>

          <td class="num">
            <template v-if="r.sold">
              <div :class="['cell-title', 'mono', r.profit.net >= 0 ? 'pos' : 'neg']">
                {{ formatSigned(r.profit.net, currency) }}
              </div>
              <div class="cell-sub">
                {{ r.profit.feesEstimated ? 'est. · ' : '' }}{{ formatPercent(r.profit.margin) }} margin
              </div>
            </template>
            <template v-else-if="r.donated">
              <div class="cell-title mono neg">-{{ formatMoney(r.item.cost, currency) }}</div>
              <div class="cell-sub">written off</div>
            </template>
            <template v-else-if="r.projected !== null">
              <div class="cell-title mono">{{ formatMoney(r.projected, currency) }}</div>
              <div class="cell-sub">est. net</div>
            </template>
            <span v-else class="dimmed">—</span>
          </td>

          <td class="col-action">
            <button
              v-if="r.onHand"
              class="item-row-sell"
              :aria-label="`Mark ${r.item.title || 'item'} as sold`"
              @click.stop="emit('sell', r.item)"
            >
              Sold
            </button>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
