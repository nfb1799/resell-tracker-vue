<script setup>
defineProps({ title: { type: String, required: true } })
</script>

<template>
  <div class="empty-state">
    <h3>{{ title }}</h3>
    <p><slot /></p>
    <slot name="action" />
  </div>
</template>
