<script setup>
import { onMounted, onUnmounted } from 'vue'

// Bottom sheet on phones, centred modal on wider screens. Locks background
// scroll and closes on Escape.
defineProps({ title: { type: String, required: true } })
const emit = defineEmits(['close'])

const onKey = (e) => { if (e.key === 'Escape') emit('close') }
let previousOverflow = ''

onMounted(() => {
  window.addEventListener('keydown', onKey)
  previousOverflow = document.body.style.overflow
  document.body.style.overflow = 'hidden'
})

onUnmounted(() => {
  window.removeEventListener('keydown', onKey)
  document.body.style.overflow = previousOverflow
})
</script>

<template>
  <div class="app-overlay" @click="emit('close')" />
  <div class="sheet" role="dialog" aria-modal="true" :aria-label="title">
    <div class="sheet-grip" />
    <div class="sheet-head">
      <h2 class="sheet-title">{{ title }}</h2>
      <button class="btn btn-ghost btn-sm" @click="emit('close')">Close</button>
    </div>
    <div class="sheet-body"><slot /></div>
    <div v-if="$slots.actions" class="sheet-actions"><slot name="actions" /></div>
  </div>
</template>
