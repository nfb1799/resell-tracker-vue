<script setup>
// A titled dashboard card: title and a quiet subtitle on the left, controls
// (a range select, a link) on the right, content below.
defineProps({
  title: { type: String, required: true },
  sub: { type: String, default: '' },
})
</script>

<template>
  <section class="card panel">
    <header class="panel-head">
      <div>
        <h2 class="panel-title">{{ title }}</h2>
        <p v-if="sub" class="panel-sub" style="margin: 0">{{ sub }}</p>
      </div>
      <slot name="actions" />
    </header>
    <slot />
  </section>
</template>
