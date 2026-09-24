<script setup lang="ts">
import { computed } from 'vue'
import type { Item } from '@/domain/item'
import { isOnHand } from '@/domain/lifecycle'
import { formatMoney, formatSigned } from '@/domain/money'
import { formatDate, itemDaysListed } from '@/lib/format'
import ItemThumb from './ItemThumb.vue'
import PlatformBadge from './PlatformBadge.vue'

// One line in any phone item list. The right-hand column changes with status: an
// asking price while listed, realised profit once sold.
//
// The row is a div wrapping two buttons rather than one big button, so the "Sold"
// shortcut can sit inside it; a button inside a button is invalid.
const props = withDefaults(defineProps<{ item: Item; currency: string; canSell?: boolean; staleAfter?: number }>(), {
  canSell: false,
  staleAfter: 45,
})
const emit = defineEmits<{ open: [item: Item]; sell: [item: Item] }>()

const days = computed(() => itemDaysListed(props.item))
const stale = computed(() => props.item.status === 'listed' && days.value !== null && days.value >= props.staleAfter)
const onHand = computed(() => isOnHand(props.item.status))
</script>

<template>
  <div class="item-row">
    <button class="item-row-main" @click="emit('open', item)">
      <ItemThumb :thumbnail="item.thumbnail" />

      <span class="item-main">
        <span class="item-title">{{ item.title || 'Untitled item' }}</span>
        <span class="item-meta">
          <PlatformBadge v-if="item.status === 'sold'" :platform="item.sale?.platform" />
          <span v-else-if="item.status === 'donated'" class="badge badge-donated">Donated</span>
          <template v-else-if="item.platforms.length">
            <PlatformBadge v-for="p in item.platforms" :key="p" :platform="p" />
          </template>
          <span v-else class="badge badge-inventory">Not listed</span>
          <span v-if="stale" class="badge badge-stale">{{ days }}d</span>
          <span v-if="item.brand">{{ item.brand }}</span>
          <span v-if="item.size">· {{ item.size }}</span>
        </span>
      </span>

      <span class="item-side">
        <template v-if="item.status === 'sold' && item.profit">
          <span :class="['item-price', item.profit.net >= 0 ? 'pos' : 'neg']">{{ formatSigned(item.profit.net, currency) }}</span>
          <span class="item-note">{{ item.profit.feesEstimated ? 'est. · ' : '' }}{{ formatDate(item.sale?.date) }}</span>
        </template>
        <template v-else-if="item.status === 'donated'">
          <span class="item-price neg">-{{ formatMoney(item.cost, currency) }}</span>
          <span class="item-note">{{ formatDate(item.donation?.date) }}</span>
        </template>
        <template v-else>
          <span class="item-price">{{ item.listPrice ? formatMoney(item.listPrice, currency) : '—' }}</span>
          <span class="item-note">
            {{ item.projectedNet !== null ? `est. net ${formatMoney(item.projectedNet, currency)}` : `cost ${formatMoney(item.cost, currency)}` }}
          </span>
        </template>
      </span>
    </button>

    <button
      v-if="onHand && canSell"
      class="item-row-sell"
      :aria-label="`Mark ${item.title || 'item'} as sold`"
      title="Mark as sold"
      @click="emit('sell', item)"
    >
      Sold
    </button>
  </div>
</template>
