<script setup lang="ts">
import { computed, ref } from 'vue'
import { formatDate } from '@/lib/format'
import { CHANGE_LABELS, conflictRows } from '@/offline/conflicts'
import type { QueuedOp } from '@/offline/db'
import { useItemsStore } from '@/stores/items'
import { useSettingsStore } from '@/stores/settings'
import { useSheetStore } from '@/stores/sheet'
import AppSheet from '../AppSheet.vue'

// Changes made offline that couldn't simply be sent. A conflict means the item
// was changed somewhere else first: the user sees both versions, field by field,
// and picks one. Nothing is overwritten or thrown away without that choice.
const items = useItemsStore()
const settings = useSettingsStore()
const sheet = useSheetStore()
const busy = ref<number | null>(null)

const entries = computed(() =>
  items.issues.map((op) => ({
    op,
    rows: op.state === 'conflict' && op.theirs ? conflictRows(op, op.theirs, settings.currency) : [],
  })),
)

async function decide(op: QueuedOp, keepMine: boolean) {
  busy.value = op.seq!
  try {
    if (keepMine) await items.keepMine(op)
    else await items.discard(op)
  } finally {
    busy.value = null
    if (items.issues.length === 0) sheet.close()
  }
}
</script>

<template>
  <AppSheet title="Needs your attention" @close="sheet.close()">
    <p class="muted note">
      These changes were made on this device while it was offline, and the item changed somewhere else in the meantime,
      or the server couldn't accept them. Choose what happens to each one.
    </p>

    <div v-for="{ op, rows } in entries" :key="op.seq" class="card conflict">
      <div class="conflict-head">
        <span class="conflict-title">{{ op.theirs?.title || op.label }}</span>
        <span class="dimmed small">{{ CHANGE_LABELS[op.kind] }} here · {{ formatDate(op.createdAt.slice(0, 10)) }}</span>
      </div>

      <template v-if="op.state === 'conflict'">
        <table class="conflict-table">
          <thead>
            <tr>
              <th scope="col"><span class="sr-only">Field</span></th>
              <th scope="col">Yours</th>
              <th scope="col">On the server now</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in rows" :key="row.field">
              <th scope="row">{{ row.field }}</th>
              <td>{{ row.yours }}</td>
              <td>{{ row.theirs }}</td>
            </tr>
          </tbody>
        </table>
        <div class="conflict-actions">
          <button class="btn btn-sm" :disabled="busy !== null" @click="decide(op, false)">Keep theirs</button>
          <button class="btn btn-sm btn-primary" :disabled="busy !== null" @click="decide(op, true)">Keep mine</button>
        </div>
      </template>

      <template v-else>
        <p class="inline-warning">{{ op.error }}</p>
        <div class="conflict-actions">
          <button class="btn btn-sm" :disabled="busy !== null" @click="decide(op, false)">Discard this change</button>
        </div>
      </template>
    </div>

    <template #actions>
      <button class="btn" @click="sheet.close()">Decide later</button>
    </template>
  </AppSheet>
</template>

<style scoped>
.note { margin: 0; font-size: 13px; }
.small { font-size: 12px; }
.conflict { display: flex; flex-direction: column; gap: 10px; }
.conflict-head { display: flex; flex-direction: column; gap: 2px; }
.conflict-title { font-weight: 600; }
.conflict-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.conflict-table th, .conflict-table td { text-align: left; padding: 6px 8px 6px 0; border-bottom: 1px solid var(--border-subtle); vertical-align: top; }
.conflict-table thead th { font-size: 11px; text-transform: uppercase; letter-spacing: 0.06em; color: var(--text-dimmed); font-weight: 600; }
.conflict-table tbody th { color: var(--text-muted); font-weight: 500; white-space: nowrap; }
.conflict-table td:nth-child(2) { color: var(--accent-primary); font-weight: 600; }
.conflict-actions { display: flex; gap: 8px; justify-content: flex-end; }
</style>
