<script setup lang="ts">
import { computed } from 'vue'
import { useItemsStore } from '@/stores/items'
import { useSheetStore } from '@/stores/sheet'
import DonateSheet from './DonateSheet.vue'
import ItemSheet from './ItemSheet.vue'
import SellSheet from './SellSheet.vue'

// Whichever sheet is open, bound to the store's current copy of its item, so a
// save elsewhere (or a reload after a conflict) is reflected immediately.
const sheet = useSheetStore()
const items = useItemsStore()

const item = computed(() => (sheet.itemId ? (items.byId.get(sheet.itemId) ?? null) : null))
// Keyed so switching from an item's editor to its sale starts a fresh form.
const key = computed(() => `${sheet.mode}:${sheet.itemId ?? 'new'}`)
</script>

<template>
  <ItemSheet v-if="sheet.mode === 'new'" :key="key" :item="null" />
  <template v-else-if="item">
    <ItemSheet v-if="sheet.mode === 'edit'" :key="key" :item="item" />
    <SellSheet v-else-if="sheet.mode === 'sell'" :key="key" :item="item" />
    <DonateSheet v-else-if="sheet.mode === 'donate'" :key="key" :item="item" />
  </template>
</template>
