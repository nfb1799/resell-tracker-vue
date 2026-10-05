<script setup>
import { isSold, isDonated } from '../../lib/status'
import AppBadge from './AppBadge.vue'
import PlatformBadge from './PlatformBadge.vue'

// Where an item stands, as badges: the platform it sold on, "Donated", the
// platforms it is live on, or "Not listed". Shared by the phone row and the
// desktop table so the two cannot disagree.
defineProps({ item: { type: Object, required: true } })
</script>

<template>
  <PlatformBadge v-if="isSold(item)" :platform="item.sale?.platform" />
  <AppBadge v-else-if="isDonated(item)" kind="donated">Donated</AppBadge>
  <template v-else-if="item.platforms?.length">
    <PlatformBadge v-for="p in item.platforms" :key="p" :platform="p" />
  </template>
  <AppBadge v-else kind="inventory">Not listed</AppBadge>
</template>
