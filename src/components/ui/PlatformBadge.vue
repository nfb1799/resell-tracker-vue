<script setup>
import { platformLabel } from '../../lib/platforms'
import AppBadge from './AppBadge.vue'

defineProps({ platform: { type: String, default: '' } })
</script>

<template>
  <AppBadge :kind="platform">{{ platformLabel(platform) }}</AppBadge>
</template>
