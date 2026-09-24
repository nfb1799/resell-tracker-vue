<script setup lang="ts">
import { platformLabel } from '@/domain/platforms'

// Coloured straight from the platform's --<id>-color token, so a platform added
// to shared/platforms.json needs only that token to get its badge colour.
defineProps<{ platform: string | null | undefined }>()
</script>

<template>
  <span class="badge" :style="{ color: `var(--${platform}-color, var(--text-muted))` }">{{ platformLabel(platform) }}</span>
</template>
