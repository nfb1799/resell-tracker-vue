<script setup lang="ts">
import { computed, ref } from 'vue'
import EmptyState from '@/components/EmptyState.vue'
import ItemRow from '@/components/ItemRow.vue'
import ItemTable from '@/components/ItemTable.vue'
import { useIsDesktop } from '@/composables/useMediaQuery'
import { add, formatMoney } from '@/domain/money'
import { PLATFORM_IDS, platformLabel } from '@/domain/platforms'
import { filterItems, SORTS, STATUS_FILTERS, statusCounts, type SortId, type StatusFilter } from '@/lib/itemFilters'
import { useItemsStore } from '@/stores/items'
import { useSettingsStore } from '@/stores/settings'
import { useSheetStore } from '@/stores/sheet'

const items = useItemsStore()
const settings = useSettingsStore()
const sheet = useSheetStore()
const isDesktop = useIsDesktop()

const status = ref<StatusFilter>('all')
const platform = ref('all')
const sort = ref<SortId>('newest')
const search = ref('')

const counts = computed(() => statusCounts(items.items))
const visible = computed(() =>
  filterItems(items.items, { status: status.value, platform: platform.value, search: search.value, sort: sort.value }),
)
const shownCost = computed(() => add(...visible.value.map((i) => i.cost)))

function clearFilters() {
  status.value = 'all'
  platform.value = 'all'
  search.value = ''
}
</script>

<template>
  <p v-if="!items.loaded" class="muted">Loading inventory…</p>

  <template v-else>
    <div class="filter-bar">
      <input v-model="search" class="input" type="search" placeholder="Search title, brand, category, notes…" aria-label="Search inventory" />

      <div class="chip-row" role="group" aria-label="Status">
        <button
          v-for="f in STATUS_FILTERS"
          :key="f.id"
          :class="['chip', { active: status === f.id }]"
          :aria-pressed="status === f.id"
          @click="status = f.id"
        >
          {{ f.label }}<span class="chip-count">{{ counts[f.id] }}</span>
        </button>
      </div>

      <div class="chip-row" role="group" aria-label="Platform">
        <button :class="['chip', { active: platform === 'all' }]" :aria-pressed="platform === 'all'" @click="platform = 'all'">
          Any platform
        </button>
        <button
          v-for="id in PLATFORM_IDS"
          :key="id"
          :class="['chip', { active: platform === id }]"
          :aria-pressed="platform === id"
          @click="platform = id"
        >
          {{ platformLabel(id) }}
        </button>
      </div>

      <div class="page-head">
        <span class="section-label">
          {{ visible.length }} item{{ visible.length === 1 ? '' : 's' }} · {{ formatMoney(shownCost, settings.currency) }} cost
        </span>
        <select v-model="sort" class="select sort-select" aria-label="Sort">
          <option v-for="(s, id) in SORTS" :key="id" :value="id">{{ s.label }}</option>
        </select>
      </div>
    </div>

    <EmptyState v-if="visible.length === 0" title="Nothing matches">
      {{ items.items.length === 0 ? 'Your inventory is empty. Add the first thing you picked up to resell.' : 'No items match these filters.' }}
      <template #action>
        <button v-if="items.items.length === 0" class="btn btn-primary" @click="sheet.open('new')">Add an item</button>
        <button v-else class="btn" @click="clearFilters">Clear filters</button>
      </template>
    </EmptyState>

    <ItemTable
      v-else-if="isDesktop"
      :items="visible"
      :currency="settings.currency"
      :sort="sort"
      @sort="sort = $event"
      @open="sheet.openItem"
      @sell="sheet.open('sell', $event)"
    />

    <div v-else class="item-list">
      <ItemRow
        v-for="item in visible"
        :key="item.id"
        :item="item"
        :currency="settings.currency"
        can-sell
        @open="sheet.openItem"
        @sell="sheet.open('sell', $event)"
      />
    </div>
  </template>
</template>

<style scoped>
.sort-select { width: auto; }
</style>
