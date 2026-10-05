<script setup>
import { computed } from 'vue'
import { formatMoney, formatSigned, computeProfit, projectedNet } from '../../lib/money'
import { formatDate, daysListed } from '../../lib/date'
import { isSold, isDonated, isOnHand } from '../../lib/status'
import AppBadge from './AppBadge.vue'
import ItemThumb from './ItemThumb.vue'
import ItemStatusBadges from './ItemStatusBadges.vue'

// One line in any item list. The right-hand column changes with status: an
// asking price while listed, realised profit once sold.
//
// The row is a div wrapping two buttons rather than one big button, so the
// "Sold" shortcut can sit inside it — a button nested in a button is invalid.
const props = defineProps({
  item: { type: Object, required: true },
  currency: { type: String, required: true },
  feeSettings: { type: Object, required: true },
  // Whether to show the "Sold" shortcut on items still on hand.
  sellable: Boolean,
  staleAfter: { type: Number, default: 45 },
})
const emit = defineEmits(['open', 'sell'])

const sold = computed(() => isSold(props.item))
const donated = computed(() => isDonated(props.item))
const onHand = computed(() => isOnHand(props.item))
const profit = computed(() => (sold.value ? computeProfit(props.item, props.feeSettings) : null))
const days = computed(() => daysListed(props.item))
const isStale = computed(() =>
  props.item.status === 'listed' && days.value !== null && days.value >= props.staleAfter)
const projected = computed(() => (onHand.value ? projectedNet(props.item, props.feeSettings) : null))
</script>

<template>
  <div class="item-row">
    <button class="item-row-main" @click="emit('open', item)">
      <ItemThumb :item="item" />

      <span class="item-main">
        <span class="item-title">{{ item.title || 'Untitled item' }}</span>
        <span class="item-meta">
          <ItemStatusBadges :item="item" />
          <AppBadge v-if="isStale" kind="stale">{{ days }}d</AppBadge>
          <span v-if="item.brand">{{ item.brand }}</span>
          <span v-if="item.size">· {{ item.size }}</span>
        </span>
      </span>

      <span class="item-side">
        <template v-if="sold">
          <span :class="['item-price', profit.net >= 0 ? 'pos' : 'neg']">
            {{ formatSigned(profit.net, currency) }}
          </span>
          <span class="item-note">
            {{ profit.feesEstimated ? 'est. · ' : '' }}{{ formatDate(item.sale?.date) }}
          </span>
        </template>
        <template v-else-if="donated">
          <span class="item-price neg">-{{ formatMoney(item.cost, currency) }}</span>
          <span class="item-note">{{ formatDate(item.donation?.date) }}</span>
        </template>
        <template v-else>
          <span class="item-price">
            {{ item.listPrice ? formatMoney(item.listPrice, currency) : '—' }}
          </span>
          <span class="item-note">
            {{ projected !== null
              ? `est. net ${formatMoney(projected, currency)}`
              : `cost ${formatMoney(item.cost, currency)}` }}
          </span>
        </template>
      </span>
    </button>

    <button
      v-if="onHand && sellable"
      class="item-row-sell"
      :aria-label="`Mark ${item.title || 'item'} as sold`"
      title="Mark as sold"
      @click="emit('sell', item)"
    >
      Sold
    </button>
  </div>
</template>
