<script setup>
import { ref, onErrorCaptured } from 'vue'

// Top-level safety net: catches render/runtime errors anywhere below it so a
// single thrown error shows a recoverable screen instead of a blank page.
const hasError = ref(false)

onErrorCaptured((error, _instance, info) => {
  console.error('Uncaught error:', error, info)
  hasError.value = true
  return false
})

const reload = () => window.location.reload()
</script>

<template>
  <div v-if="hasError" class="error-boundary">
    <div class="card error-boundary-card">
      <h2>Something went wrong</h2>
      <p class="muted">The app hit an unexpected error. Reloading usually fixes it.</p>
      <button class="btn btn-primary" @click="reload">Reload</button>
    </div>
  </div>
  <slot v-else />
</template>
