<script setup>
defineProps({ kind: { type: String, required: true } })
</script>

<template>
  <span :class="`badge badge-${kind}`"><slot /></span>
</template>
