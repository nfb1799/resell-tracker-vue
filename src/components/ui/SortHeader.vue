<script setup>
defineProps({
  label: { type: String, required: true },
  active: Boolean,
  align: { type: String, default: '' },
})
const emit = defineEmits(['click'])
</script>

<template>
  <th :class="{ num: align === 'right' }">
    <button :class="['th-sort', { active }]" @click="emit('click')">
      {{ label }}<span class="th-caret" aria-hidden="true">{{ active ? '▾' : '' }}</span>
    </button>
  </th>
</template>
