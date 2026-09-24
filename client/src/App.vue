<script setup lang="ts">
import { onErrorCaptured, ref } from 'vue'
import { RouterView, useRoute } from 'vue-router'
import AppShell from '@/components/shell/AppShell.vue'
import ToastHost from '@/components/ToastHost.vue'

const route = useRoute()

// A render error anywhere shows a way out instead of a blank page.
const crashed = ref(false)
const reload = () => window.location.reload()
onErrorCaptured((error) => {
  console.error(error)
  crashed.value = true
  return false
})
</script>

<template>
  <div v-if="crashed" class="error-boundary">
    <div class="card error-boundary-card">
      <h2>Something went wrong</h2>
      <p class="muted">The app hit an unexpected error. Reloading usually fixes it.</p>
      <button class="btn btn-primary" @click="reload">Reload</button>
    </div>
  </div>
  <template v-else-if="route.matched.length">
    <RouterView v-if="route.meta.public" />
    <AppShell v-else />
  </template>
  <ToastHost />
</template>
