<script setup lang="ts">
import { ref, watch } from 'vue'
import { useConnectionStore } from '@/stores/connection'
import { useItemsStore } from '@/stores/items'

// Reassurance rather than a warning: anything logged in a dead zone is kept on
// the device and sent when the connection is back.
const connection = useConnectionStore()
const items = useItemsStore()
const showBack = ref(false)
let timer: ReturnType<typeof setTimeout> | undefined

watch(
  () => connection.online,
  (online) => {
    clearTimeout(timer)
    showBack.value = online
    if (online) timer = setTimeout(() => (showBack.value = false), 3000)
  },
)
</script>

<template>
  <div v-if="!connection.online" class="offline-indicator offline" role="status">
    Offline — changes save and sync later<template v-if="items.pendingCount"> · {{ items.pendingCount }} waiting</template>
  </div>
  <div v-else-if="showBack" class="offline-indicator online" role="status">
    {{
      items.pendingCount
        ? `Back online — syncing ${items.pendingCount}…`
        : items.issues.length
          ? `Back online — ${items.issues.length} change${items.issues.length === 1 ? ' needs' : 's need'} your attention`
          : 'Back online — synced'
    }}
  </div>
</template>
