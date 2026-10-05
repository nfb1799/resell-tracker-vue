<script setup>
import { ref, onMounted, onUnmounted } from 'vue'

// Firestore queues writes locally while offline, so this is reassurance rather
// than a warning: nothing you log in a dead zone is lost.
const isOnline = ref(navigator.onLine)
const showBanner = ref(false)
let timer = null

const handleOnline = () => {
  isOnline.value = true
  showBanner.value = true
  clearTimeout(timer)
  timer = setTimeout(() => { showBanner.value = false }, 3000)
}
const handleOffline = () => {
  clearTimeout(timer)
  isOnline.value = false
  showBanner.value = true
}

onMounted(() => {
  window.addEventListener('online', handleOnline)
  window.addEventListener('offline', handleOffline)
})
onUnmounted(() => {
  clearTimeout(timer)
  window.removeEventListener('online', handleOnline)
  window.removeEventListener('offline', handleOffline)
})
</script>

<template>
  <div v-if="showBanner || !isOnline" :class="['offline-indicator', isOnline ? 'online' : 'offline']">
    {{ isOnline ? 'Back online — synced' : 'Offline — changes save and sync later' }}
  </div>
</template>
