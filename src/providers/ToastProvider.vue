<script setup>
import { ref, provide } from 'vue'
import { ToastKey } from '../composables/keys'

const toasts = ref([])
let nextId = 0

function showToast(message, type = 'info') {
  const id = ++nextId
  toasts.value.push({ id, message, type })
  setTimeout(() => {
    toasts.value = toasts.value.filter(t => t.id !== id)
  }, 3500)
}

provide(ToastKey, showToast)
</script>

<template>
  <slot />
  <div class="toast-container" role="status" aria-live="polite">
    <div v-for="t in toasts" :key="t.id" :class="`toast toast-${t.type}`">
      {{ t.message }}
    </div>
  </div>
</template>
