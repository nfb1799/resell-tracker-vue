<script setup>
import { ref, computed } from 'vue'
import { useItems } from '../composables/useItems'
import { useToast } from '../composables/useToast'
import { parseImport } from '../lib/importItems'
import { photoFromSource } from '../lib/image'
import { platformLabel } from '../lib/platforms'
import { formatMoney } from '../lib/money'
import BottomSheet from './ui/BottomSheet.vue'

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

// Adds many items at once from pasted JSON or a .json file.
//
// It does not write anything itself: every row goes through the same addItem and
// setPhoto the "New item" form uses, one at a time, so imported items carry the
// same defaults and the same computed figures as hand-added ones. A row that
// fails validation is skipped and reported; the rest still go in.
const emit = defineEmits(['close'])
const { addItem, setPhoto, currency } = useItems()
const showToast = useToast()

const text = ref('')
const fileName = ref('')
const progress = ref(null)
const result = ref(null)
const fileInput = ref(null)

// Re-validated as you type, so problems surface before you commit to anything.
const parsed = computed(() => (text.value.trim() ? parseImport(text.value) : null))
const importing = computed(() => progress.value !== null)
const canImport = computed(() => !importing.value && parsed.value && !parsed.value.fatal && parsed.value.valid.length > 0)

async function handleFile(e) {
  const file = e.target.files?.[0]
  e.target.value = ''
  if (!file) return
  try {
    text.value = await file.text()
    fileName.value = file.name
    result.value = null
  } catch (error) {
    console.error(error)
    showToast('Could not read that file', 'error')
  }
}

function onType(e) {
  text.value = e.target.value
  fileName.value = ''
}

async function handleImport() {
  const run = parsed.value
  if (!run || run.fatal || run.valid.length === 0) return

  const added = []
  const photoWarnings = []
  const skipped = [...run.invalid]
  result.value = null

  for (const [index, row] of run.valid.entries()) {
    progress.value = { done: index, total: run.valid.length, label: row.label }

    let photo = null
    if (row.photo) {
      try {
        photo = await photoFromSource(row.photo.src)
      } catch (error) {
        // A photo we cannot fetch is not a reason to lose the item.
        photoWarnings.push({ label: row.label, reason: error.message })
      }
    }

    try {
      const id = await addItem(row.item, { notify: false })
      if (photo) await setPhoto(id, photo)
      added.push(row.label)
    } catch (error) {
      console.error(error)
      skipped.push({
        rowNumber: row.rowNumber,
        label: row.label,
        errors: [`could not be saved: ${error.message}`],
      })
    }
  }

  progress.value = null
  result.value = { added, photoWarnings, skipped }
}

function reset() {
  text.value = ''
  fileName.value = ''
  result.value = null
}

const previewLine = (row) => [
  row.item.platforms.map(platformLabel).join(' + ') || 'not listed',
  `cost ${formatMoney(row.item.cost, currency.value)}`,
  row.item.listPrice ? `asking ${formatMoney(row.item.listPrice, currency.value)}` : null,
  row.photo ? 'photo' : null,
].filter(Boolean).join(' · ')
</script>

<template>
  <!-- after the run: the summary, not a silent redirect -->
  <BottomSheet v-if="result" title="Import finished" @close="emit('close')">
    <div class="import-summary">
      <span class="import-count pos">{{ result.added.length }} added</span>
      <span v-if="result.skipped.length > 0" class="import-count neg">{{ result.skipped.length }} skipped</span>
      <span v-if="result.photoWarnings.length > 0" class="import-count muted">
        {{ result.photoWarnings.length }} without a photo
      </span>
    </div>

    <div v-if="result.skipped.length > 0" class="card">
      <span class="section-label">Skipped</span>
      <ul class="import-issues">
        <li v-for="row in result.skipped" :key="row.rowNumber">
          <strong>Row {{ row.rowNumber }}</strong>
          {{ row.label !== `Row ${row.rowNumber}` ? `· ${row.label}` : '' }}
          <ul><li v-for="e in row.errors" :key="e">{{ e }}</li></ul>
        </li>
      </ul>
    </div>

    <div v-if="result.photoWarnings.length > 0" class="card">
      <span class="section-label">Imported without a photo</span>
      <ul class="import-issues">
        <li v-for="w in result.photoWarnings" :key="w.label"><strong>{{ w.label }}</strong> · {{ w.reason }}</li>
      </ul>
      <p class="muted" style="margin: 8px 0 0; font-size: 12.5px">
        The item is in — only the image could not be fetched, usually because
        that host blocks other sites from reading its files. Open the item and
        add the photo yourself, or paste it as a data URI instead.
      </p>
    </div>

    <div v-if="result.added.length > 0" class="card">
      <span class="section-label">Added</span>
      <ul class="import-issues">
        <li v-for="(label, i) in result.added" :key="i">{{ label }}</li>
      </ul>
    </div>

    <template #actions>
      <button class="btn" @click="reset">Import more</button>
      <button class="btn btn-primary" @click="emit('close')">Done</button>
    </template>
  </BottomSheet>

  <!-- before the run: paste, check, import -->
  <BottomSheet v-else title="Bulk import" @close="emit('close')">
    <p class="muted" style="margin: 0; font-size: 13px">
      Paste a JSON array of items, or choose a <code>.json</code> file. Every row
      goes in exactly as if you had typed it into the New item form.
    </p>

    <div class="photo-actions" style="flex-direction: row; align-items: center">
      <input ref="fileInput" type="file" accept=".json,application/json" hidden @change="handleFile" />
      <button class="btn btn-sm" :disabled="importing" @click="fileInput?.click()">
        Choose .json file
      </button>
      <span v-if="fileName" class="dimmed" style="font-size: 12px">{{ fileName }}</span>
      <button v-if="text && !importing" class="btn btn-sm btn-ghost" @click="reset">Clear</button>
    </div>

    <div class="field">
      <label for="import-json">JSON</label>
      <textarea
        id="import-json"
        class="textarea import-textarea mono"
        :value="text"
        :placeholder="SAMPLE"
        spellcheck="false"
        :disabled="importing"
        @input="onType"
      />
    </div>

    <p v-if="parsed?.fatal" class="inline-warning">{{ parsed.fatal }}</p>

    <template v-if="parsed && !parsed.fatal">
      <div class="import-summary">
        <span class="import-count pos">{{ parsed.valid.length }} ready</span>
        <span v-if="parsed.invalid.length > 0" class="import-count neg">
          {{ parsed.invalid.length }} with problems
        </span>
      </div>

      <div v-if="parsed.invalid.length > 0" class="card">
        <span class="section-label">These rows will be skipped</span>
        <ul class="import-issues">
          <li v-for="row in parsed.invalid" :key="row.rowNumber">
            <strong>Row {{ row.rowNumber }}</strong>
            {{ row.label !== `Row ${row.rowNumber}` ? ` · ${row.label}` : '' }}
            <ul><li v-for="e in row.errors" :key="e">{{ e }}</li></ul>
          </li>
        </ul>
      </div>

      <div v-if="parsed.valid.length > 0" class="card">
        <span class="section-label">Ready to import</span>
        <ul class="import-preview">
          <li v-for="row in parsed.valid.slice(0, 8)" :key="row.rowNumber">
            <span class="import-preview-title">{{ row.item.title }}</span>
            <span class="dimmed">{{ previewLine(row) }}</span>
          </li>
          <li v-if="parsed.valid.length > 8" class="dimmed">…and {{ parsed.valid.length - 8 }} more</li>
        </ul>
      </div>
    </template>

    <p v-if="importing" class="muted" style="margin: 0; font-size: 13px">
      Adding {{ progress.label }}…
    </p>

    <template #actions>
      <button class="btn" :disabled="importing" @click="emit('close')">Cancel</button>
      <button class="btn btn-primary" :disabled="!canImport" @click="handleImport">
        {{ importing
          ? `Importing ${progress.done + 1} of ${progress.total}…`
          : parsed && !parsed.fatal
            ? `Import ${parsed.valid.length} item${parsed.valid.length === 1 ? '' : 's'}`
            : 'Import' }}
      </button>
    </template>
  </BottomSheet>
</template>
