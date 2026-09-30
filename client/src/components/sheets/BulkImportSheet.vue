<script setup lang="ts">
import { computed, ref, useTemplateRef } from 'vue'
import { ApiError } from '@/api'
import { formatMoney, fromDollars } from '@/domain/money'
import { platformLabel } from '@/domain/platforms'
import { photoFromSource } from '@/lib/image'
import { parseImport, withoutPhotos, type ImportRow } from '@/lib/importItems'
import { useConnectionStore } from '@/stores/connection'
import { useItemsStore } from '@/stores/items'
import { useSettingsStore } from '@/stores/settings'
import { useSheetStore } from '@/stores/sheet'
import { useToastStore } from '@/stores/toast'
import AppSheet from '../AppSheet.vue'

// Adds many items at once from pasted JSON or a .json file (a backup export
// included). Problems show as you type; the server checks every row again and
// adds the good ones through the same path the New item form uses. Photos are
// fetched and resized here afterwards, exactly as an uploaded one would be.

const SAMPLE = `[
  {
    "title": "Nautica Polo",
    "brand": "Nautica",
    "size": "XL",
    "category": "Shirt",
    "condition": "Good",
    "cost": 0,
    "acquiredDate": "2026-08-28",
    "sourcedFrom": "Dad",
    "listingPlatform": "Depop",
    "askingPrice": 20,
    "listedDate": "2026-08-28",
    "notes": "",
    "photo": "https://example.com/photo.jpg"
  }
]`

interface Outcome {
  added: string[]
  skipped: { rowNumber: number; label: string; errors: string[] }[]
  photoWarnings: { label: string; reason: string }[]
}

const items = useItemsStore()
const connection = useConnectionStore()
const settings = useSettingsStore()
const sheet = useSheetStore()
const toast = useToastStore()

const input = ref('')
const fileName = ref('')
const progress = ref<string | null>(null)
const outcome = ref<Outcome | null>(null)
const fileInput = useTemplateRef<HTMLInputElement>('file')

// Re-checked as you type, so problems surface before anything is written.
const parsed = computed(() => (input.value.trim() ? parseImport(input.value) : null))
const ready = computed(() => (parsed.value && parsed.value.fatal === null ? parsed.value : null))
const importing = computed(() => progress.value !== null)

async function pickFile(event: Event) {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  target.value = ''
  if (!file) return
  try {
    input.value = await file.text()
    fileName.value = file.name
    outcome.value = null
  } catch {
    toast.show('Could not read that file', 'error')
  }
}

function describe(row: ImportRow): string {
  const item = row.item!
  const money = (dollars: number) => formatMoney(fromDollars(dollars), settings.currency)
  return [
    item.status === 'sold' ? 'sold' : item.status === 'donated' ? 'donated' : item.platforms.map(platformLabel).join(' + ') || 'not listed',
    `cost ${money(item.cost)}`,
    item.listPrice ? `asking ${money(item.listPrice)}` : null,
    row.photo ? 'photo' : null,
  ]
    .filter(Boolean)
    .join(' · ')
}

async function runImport() {
  const plan = ready.value
  if (!plan || plan.valid.length === 0) return
  if (!connection.online) {
    toast.show('Bulk import needs a connection; the server checks every row. Try again when you are back online.', 'error')
    return
  }

  progress.value = `Importing ${plan.valid.length} item${plan.valid.length === 1 ? '' : 's'}…`
  try {
    const result = await items.importRows(withoutPhotos(plan.raw))
    const done: Outcome = {
      added: result.rows.filter((r) => r.status === 'added').map((r) => r.label),
      skipped: result.rows.filter((r) => r.status === 'skipped'),
      photoWarnings: [],
    }

    // Photos last: an image we can't fetch is a warning, never a reason to lose the item.
    const withPhotos = plan.rows.filter((r) => r.photo && result.added.has(r.rowNumber))
    for (const [index, row] of withPhotos.entries()) {
      progress.value = `Adding photo ${index + 1} of ${withPhotos.length}…`
      try {
        const photo = await photoFromSource(row.photo!.src)
        await items.setPhoto(result.added.get(row.rowNumber)!, photo.thumbnail, photo.full)
      } catch (error) {
        done.photoWarnings.push({ label: row.label, reason: error instanceof Error ? error.message : 'could not be read' })
      }
    }

    outcome.value = done
  } catch (error) {
    toast.show(error instanceof ApiError ? error.message : 'The import failed. Check your connection and try again.', 'error')
  } finally {
    progress.value = null
  }
}

function reset() {
  input.value = ''
  fileName.value = ''
  outcome.value = null
}
</script>

<template>
  <!-- After the run: the summary, never a silent redirect. -->
  <AppSheet v-if="outcome" title="Import finished" @close="sheet.close()">
    <div class="import-summary">
      <span class="import-count pos">{{ outcome.added.length }} added</span>
      <span v-if="outcome.skipped.length" class="import-count neg">{{ outcome.skipped.length }} skipped</span>
      <span v-if="outcome.photoWarnings.length" class="import-count muted">{{ outcome.photoWarnings.length }} without a photo</span>
    </div>

    <div v-if="outcome.skipped.length" class="card">
      <span class="section-label">Skipped</span>
      <ul class="import-issues">
        <li v-for="row in outcome.skipped" :key="row.rowNumber">
          <strong>Row {{ row.rowNumber }}</strong><template v-if="row.label !== `Row ${row.rowNumber}`"> · {{ row.label }}</template>
          <ul>
            <li v-for="e in row.errors" :key="e">{{ e }}</li>
          </ul>
        </li>
      </ul>
    </div>

    <div v-if="outcome.photoWarnings.length" class="card">
      <span class="section-label">Imported without a photo</span>
      <ul class="import-issues">
        <li v-for="w in outcome.photoWarnings" :key="w.label"><strong>{{ w.label }}</strong> · {{ w.reason }}</li>
      </ul>
      <p class="muted note">
        The item is in — only the image could not be fetched, usually because that host blocks other sites from reading
        its files. Open the item and add the photo yourself, or paste it as a data URI instead.
      </p>
    </div>

    <div v-if="outcome.added.length" class="card">
      <span class="section-label">Added</span>
      <ul class="import-issues">
        <li v-for="(label, i) in outcome.added" :key="i">{{ label }}</li>
      </ul>
    </div>

    <template #actions>
      <button class="btn" @click="reset">Import more</button>
      <button class="btn btn-primary" @click="sheet.close()">Done</button>
    </template>
  </AppSheet>

  <!-- Before the run: paste, check, import. -->
  <AppSheet v-else title="Bulk import" @close="importing || sheet.close()">
    <p class="muted note">
      Paste a JSON array of items, or choose a <code>.json</code> file (a backup from Settings works too). Every row goes
      in exactly as if you had typed it into the New item form.
    </p>

    <div class="photo-actions file-row">
      <input ref="file" type="file" accept=".json,application/json" hidden @change="pickFile" />
      <button class="btn btn-sm" :disabled="importing" @click="fileInput?.click()">Choose .json file</button>
      <span v-if="fileName" class="dimmed small">{{ fileName }}</span>
      <button v-if="input && !importing" class="btn btn-sm btn-ghost" @click="reset">Clear</button>
    </div>

    <div class="field">
      <label for="import-json">JSON</label>
      <textarea
        id="import-json"
        v-model="input"
        class="textarea import-textarea mono"
        :placeholder="SAMPLE"
        spellcheck="false"
        :disabled="importing"
        @input="fileName = ''"
      />
    </div>

    <p v-if="parsed && parsed.fatal" class="inline-warning">{{ parsed.fatal }}</p>

    <template v-if="ready">
      <div class="import-summary" aria-live="polite">
        <span class="import-count pos">{{ ready.valid.length }} ready</span>
        <span v-if="ready.invalid.length" class="import-count neg">{{ ready.invalid.length }} with problems</span>
      </div>

      <div v-if="ready.invalid.length" class="card">
        <span class="section-label">These rows will be skipped</span>
        <ul class="import-issues">
          <li v-for="row in ready.invalid" :key="row.rowNumber">
            <strong>Row {{ row.rowNumber }}</strong><template v-if="row.label !== `Row ${row.rowNumber}`"> · {{ row.label }}</template>
            <ul>
              <li v-for="e in row.errors" :key="e">{{ e }}</li>
            </ul>
          </li>
        </ul>
      </div>

      <div v-if="ready.valid.length" class="card">
        <span class="section-label">Ready to import</span>
        <ul class="import-preview">
          <li v-for="row in ready.valid.slice(0, 8)" :key="row.rowNumber">
            <span class="import-preview-title">{{ row.item?.title }}</span>
            <span class="dimmed">{{ describe(row) }}</span>
          </li>
          <li v-if="ready.valid.length > 8" class="dimmed">…and {{ ready.valid.length - 8 }} more</li>
        </ul>
      </div>
    </template>

    <p v-if="progress" class="muted note" role="status">{{ progress }}</p>

    <template #actions>
      <button class="btn" :disabled="importing" @click="sheet.close()">Cancel</button>
      <button class="btn btn-primary" :disabled="importing || !ready || ready.valid.length === 0" @click="runImport">
        {{ importing ? 'Importing…' : ready ? `Import ${ready.valid.length} item${ready.valid.length === 1 ? '' : 's'}` : 'Import' }}
      </button>
    </template>
  </AppSheet>
</template>

<style scoped>
.note { margin: 0; font-size: 13px; }
.file-row { flex-direction: row; align-items: center; }
.small { font-size: 12px; }
</style>
