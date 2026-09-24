<script setup lang="ts">
import { useToastStore } from '@/stores/toast'

const toasts = useToastStore()
</script>

<template>
  <div class="toast-container" role="status" aria-live="polite">
    <div v-for="t in toasts.toasts" :key="t.id" :class="['toast', `toast-${t.kind}`]">{{ t.message }}</div>
  </div>
</template>
