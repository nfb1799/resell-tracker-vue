<script setup>
import { ref, computed } from 'vue'
import { useItems } from '../composables/useItems'
import { useIsDesktop } from '../composables/useMediaQuery'
import { PLATFORM_IDS, platformLabel } from '../lib/platforms'
import { formatMoney, num } from '../lib/money'
import { daysListed } from '../lib/date'
import ItemTable from './ItemTable.vue'
import ItemRow from './ui/ItemRow.vue'
import EmptyState from './ui/EmptyState.vue'

const STATUS_FILTERS = [
  { id: 'all', label: 'All' },
  { id: 'listed', label: 'Listed' },
  { id: 'inventory', label: 'In stock' },
  { id: 'sold', label: 'Sold' },
  { id: 'donated', label: 'Donated' },
]

const SORTS = {
  newest: { label: 'Newest first', compare: (a, b) => (b.createdAt || '').localeCompare(a.createdAt || '') },
  oldest: { label: 'Longest listed', compare: (a, b) => (daysListed(b) ?? -1) - (daysListed(a) ?? -1) },
  priceHigh: { label: 'Price: high to low', compare: (a, b) => num(b.listPrice) - num(a.listPrice) },
  priceLow: { label: 'Price: low to high', compare: (a, b) => num(a.listPrice) - num(b.listPrice) },
  title: { label: 'Title A–Z', compare: (a, b) => (a.title || '').localeCompare(b.title || '') },
}

const matchesSearch = (item, needle) => {
  if (!needle) return true
  const haystack = [item.title, item.brand, item.category, item.size, item.source, item.notes, item.donation?.org]
    .filter(Boolean).join(' ').toLowerCase()
  return haystack.includes(needle)
}

const emit = defineEmits(['open', 'sell', 'add', 'bulk-import'])
const { items, loading, currency, feeSettings } = useItems()
const isDesktop = useIsDesktop()
const status = ref('all')
const platform = ref('all')
const sort = ref('newest')
const search = ref('')

const counts = computed(() => ({
  all: items.value.length,
  listed: items.value.filter(i => i.status === 'listed').length,
  inventory: items.value.filter(i => i.status === 'inventory').length,
  sold: items.value.filter(i => i.status === 'sold').length,
  donated: items.value.filter(i => i.status === 'donated').length,
}))

const visible = computed(() => {
  const needle = search.value.trim().toLowerCase()
  return items.value
    .filter(item => {
      if (status.value !== 'all' && item.status !== status.value) return false
      if (platform.value !== 'all') {
        // A sold item belongs to the platform it actually sold on; a donated
        // one keeps whatever it was listed on before it was given up on.
        const on = item.status === 'sold'
          ? [item.sale?.platform]
          : (item.platforms || [])
        if (!on.includes(platform.value)) return false
      }
      return matchesSearch(item, needle)
    })
    .sort(SORTS[sort.value].compare)
})

const shownCost = computed(() => visible.value.reduce((sum, i) => sum + num(i.cost), 0))

function clearFilters() {
  status.value = 'all'
  platform.value = 'all'
  search.value = ''
}
</script>

<template>
  <p v-if="loading" class="muted">Loading inventory…</p>

  <template v-else>
    <div class="filter-bar">
      <input v-model="search" class="input" type="search" placeholder="Search title, brand, category, notes…" />

      <div class="chip-row">
        <button v-for="f in STATUS_FILTERS" :key="f.id" :class="['chip', { active: status === f.id }]"
          @click="status = f.id">
          {{ f.label }}<span class="chip-count">{{ counts[f.id] }}</span>
        </button>
      </div>

      <div class="chip-row">
        <button :class="['chip', { active: platform === 'all' }]" @click="platform = 'all'">
          Any platform
        </button>
        <button v-for="id in PLATFORM_IDS" :key="id" :class="['chip', { active: platform === id }]"
          @click="platform = id">
          {{ platformLabel(id) }}
        </button>
      </div>

      <div class="page-head">
        <span class="section-label">
          {{ visible.length }} item{{ visible.length === 1 ? '' : 's' }} · {{ formatMoney(shownCost, currency) }} cost
        </span>
        <div style="display: flex; gap: 8px; align-items: center">
          <!-- On desktop the page header carries Bulk import. -->
          <button v-if="!isDesktop" class="btn btn-sm" @click="emit('bulk-import')">Bulk import</button>
          <select v-model="sort" class="range-select" aria-label="Sort">
            <option v-for="(s, id) in SORTS" :key="id" :value="id">{{ s.label }}</option>
          </select>
        </div>
      </div>
    </div>

    <EmptyState v-if="visible.length === 0" title="Nothing matches">
      {{ items.length === 0
        ? 'Your inventory is empty. Add the first thing you picked up to resell.'
        : 'No items match these filters.' }}
      <template #action>
        <button v-if="items.length === 0" class="btn btn-primary" @click="emit('add')">Add an item</button>
        <button v-else class="btn" @click="clearFilters">Clear filters</button>
      </template>
    </EmptyState>

    <ItemTable
      v-else-if="isDesktop"
      :items="visible"
      :currency="currency"
      :fee-settings="feeSettings"
      :sort="sort"
      @sort="sort = $event"
      @open="emit('open', $event)"
      @sell="emit('sell', $event)"
    />

    <div v-else class="item-list">
      <ItemRow v-for="item in visible" :key="item.id" :item="item" :currency="currency"
        :fee-settings="feeSettings" sellable
        @open="emit('open', $event)" @sell="emit('sell', $event)" />
    </div>
  </template>
</template>
