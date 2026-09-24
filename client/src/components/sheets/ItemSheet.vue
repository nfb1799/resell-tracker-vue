<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, useTemplateRef } from 'vue'
import { ApiError, itemsApi } from '@/api'
import { getLocalDateString } from '@/domain/dates'
import { CONDITIONS, DEFAULT_CONDITION, type Item, type ItemFields } from '@/domain/item'
import { applyPlatforms, isOnHand, type ItemStatus } from '@/domain/lifecycle'
import { formatMoney, parseMoney, toDollars, ZERO, type Cents } from '@/domain/money'
import { PLATFORM_IDS, platformLabel } from '@/domain/platforms'
import { projectedNet } from '@/domain/profit'
import { processPhoto, type ProcessedPhoto } from '@/lib/image'
import { useItemsStore } from '@/stores/items'
import { useSettingsStore } from '@/stores/settings'
import { useSheetStore } from '@/stores/sheet'
import { useToastStore } from '@/stores/toast'
import AppSheet from '../AppSheet.vue'
import BreakdownRow from '../BreakdownRow.vue'

// Creates an item or edits one's own fields. Status is never picked: adding the
// first platform lists it, removing the last puts it back in stock, exactly as
// the server will decide (see domain/lifecycle).
const props = defineProps<{ item: Item | null }>()

const items = useItemsStore()
const settings = useSettingsStore()
const sheet = useSheetStore()
const toast = useToastStore()

const isNew = !props.item
const moneyInput = (c: Cents | null | undefined) => (c === null || c === undefined ? '' : String(toDollars(c)))

const form = reactive({
  title: props.item?.title ?? '',
  brand: props.item?.brand ?? '',
  category: props.item?.category ?? '',
  size: props.item?.size ?? '',
  condition: props.item?.condition ?? DEFAULT_CONDITION,
  notes: props.item?.notes ?? '',
  cost: moneyInput(props.item?.cost),
  source: props.item?.source ?? '',
  acquiredDate: props.item ? (props.item.acquiredDate ?? '') : getLocalDateString(),
  status: (props.item?.status ?? 'inventory') as ItemStatus,
  platforms: [...(props.item?.platforms ?? [])],
  listPrice: moneyInput(props.item?.listPrice),
  listedDate: props.item?.listedDate ?? '',
})

const saving = ref(false)
const confirmDelete = ref(false)
const busyPhoto = ref(false)
const pending = ref<(ProcessedPhoto & { url: string }) | null>(null)
const photoCleared = ref(false)
const fileInput = useTemplateRef<HTMLInputElement>('file')

// The saved photo is fetched only now that this one item is open.
const preview = computed(() => {
  if (pending.value) return pending.value.url
  if (photoCleared.value || !props.item?.thumbnail) return ''
  return itemsApi.photoUrl(props.item.id, props.item.version)
})

onBeforeUnmount(() => {
  if (pending.value) URL.revokeObjectURL(pending.value.url)
})

// Suggest categories and sources already used, so they stay consistently spelled.
const knownCategories = computed(() => [...new Set(items.items.map((i) => i.category).filter(Boolean))].sort())
const knownSources = computed(() => [...new Set(items.items.map((i) => i.source).filter(Boolean))].sort())

const projected = computed(() =>
  projectedNet(
    { listPrice: parseMoney(form.listPrice), cost: parseMoney(form.cost) ?? ZERO, platforms: form.platforms },
    settings.fees,
  ),
)

function togglePlatform(id: string) {
  const platforms = form.platforms.includes(id) ? form.platforms.filter((p) => p !== id) : [...form.platforms, id]
  const next = applyPlatforms(form.status, platforms, form.listedDate || null, getLocalDateString())
  form.platforms = platforms
  form.status = next.status
  form.listedDate = next.listedDate ?? ''
}

async function pickPhoto(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = '' // so picking the same file twice still fires a change
  if (!file) return
  busyPhoto.value = true
  try {
    const photo = await processPhoto(file)
    if (pending.value) URL.revokeObjectURL(pending.value.url)
    pending.value = { ...photo, url: URL.createObjectURL(photo.full) }
    photoCleared.value = false
  } catch (error) {
    toast.show(error instanceof Error ? error.message : 'Could not read that image', 'error')
  } finally {
    busyPhoto.value = false
  }
}

function clearPhoto() {
  if (pending.value) URL.revokeObjectURL(pending.value.url)
  pending.value = null
  photoCleared.value = true
}

function fields(): ItemFields {
  return {
    title: form.title.trim(),
    brand: form.brand,
    category: form.category,
    size: form.size,
    condition: form.condition,
    notes: form.notes,
    cost: parseMoney(form.cost) ?? ZERO,
    source: form.source,
    acquiredDate: form.acquiredDate || null,
    platforms: form.platforms,
    listPrice: parseMoney(form.listPrice),
    listedDate: form.listedDate || null,
  }
}

async function save() {
  if (!form.title.trim()) {
    toast.show('Give the item a title', 'error')
    return
  }
  saving.value = true
  try {
    let saved = props.item ? await items.update(props.item, fields()) : await items.create(fields())
    if (pending.value) saved = await items.setPhoto(saved, pending.value.thumbnail, pending.value.full)
    else if (photoCleared.value && saved.thumbnail) await items.removePhoto(saved)
    if (!isNew) toast.show('Saved', 'success')
    sheet.close()
  } catch (error) {
    if (!(error instanceof ApiError && error.isStale)) {
      toast.show(error instanceof ApiError ? error.message : 'Could not save item', 'error')
    }
    saving.value = false
  }
}

async function remove() {
  if (!props.item) return
  if (!confirmDelete.value) {
    confirmDelete.value = true
    return
  }
  try {
    await items.remove(props.item)
    sheet.close()
  } catch {
    toast.show('Could not delete item', 'error')
  }
}
</script>

<template>
  <AppSheet :title="isNew ? 'New item' : form.title || 'Edit item'" @close="sheet.close()">
    <div class="photo-field">
      <div class="photo-frame">
        <img v-if="preview" :src="preview" :alt="form.title || 'Item photo'" />
        <span v-else class="photo-empty">No photo</span>
      </div>
      <div class="photo-actions">
        <input ref="file" type="file" accept="image/*" capture="environment" hidden @change="pickPhoto" />
        <button class="btn btn-sm" :disabled="busyPhoto" @click="fileInput?.click()">
          {{ busyPhoto ? 'Working…' : preview ? 'Replace photo' : 'Add photo' }}
        </button>
        <button v-if="preview" class="btn btn-sm btn-ghost" :disabled="busyPhoto" @click="clearPhoto">Remove</button>
      </div>
    </div>

    <div class="field">
      <label for="title">Title</label>
      <input id="title" v-model="form.title" class="input" placeholder="Carhartt detroit jacket" :autofocus="isNew" />
    </div>

    <div class="field-row">
      <div class="field">
        <label for="brand">Brand</label>
        <input id="brand" v-model="form.brand" class="input" />
      </div>
      <div class="field">
        <label for="size">Size</label>
        <input id="size" v-model="form.size" class="input" />
      </div>
    </div>

    <div class="field-row">
      <div class="field">
        <label for="category">Category</label>
        <input id="category" v-model="form.category" class="input" list="known-categories" />
        <datalist id="known-categories">
          <option v-for="c in knownCategories" :key="c" :value="c" />
        </datalist>
      </div>
      <div class="field">
        <label for="condition">Condition</label>
        <select id="condition" v-model="form.condition" class="select">
          <option v-for="c in CONDITIONS" :key="c" :value="c">{{ c }}</option>
        </select>
      </div>
    </div>

    <span class="section-label">Cost of goods</span>

    <div class="field-row">
      <div class="field">
        <label for="cost">What you paid</label>
        <input id="cost" v-model="form.cost" class="input mono" type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" />
      </div>
      <div class="field">
        <label for="acquired">Acquired</label>
        <input id="acquired" v-model="form.acquiredDate" class="input" type="date" />
      </div>
    </div>

    <div class="field">
      <label for="source">Sourced from</label>
      <input id="source" v-model="form.source" class="input" list="known-sources" placeholder="Goodwill on Jefferson" />
      <datalist id="known-sources">
        <option v-for="s in knownSources" :key="s" :value="s" />
      </datalist>
    </div>

    <span class="section-label">Listing</span>

    <div class="chip-row">
      <button
        v-for="id in PLATFORM_IDS"
        :key="id"
        type="button"
        :class="['chip', { active: form.platforms.includes(id) }]"
        :aria-pressed="form.platforms.includes(id)"
        @click="togglePlatform(id)"
      >
        {{ platformLabel(id) }}
      </button>
    </div>

    <div class="field-row">
      <div class="field">
        <label for="listPrice">Asking price</label>
        <input id="listPrice" v-model="form.listPrice" class="input mono" type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" />
      </div>
      <div class="field">
        <label for="listedDate">Listed on</label>
        <input id="listedDate" v-model="form.listedDate" class="input" type="date" />
      </div>
    </div>

    <div v-if="projected !== null" class="breakdown">
      <BreakdownRow
        :label="`Est. net if it sells at asking on ${platformLabel(form.platforms[0] || 'other')}`"
        :value="formatMoney(projected, settings.currency)"
        :tone="projected >= 0 ? 'pos' : 'neg'"
      />
    </div>

    <div class="field">
      <label for="notes">Notes</label>
      <textarea id="notes" v-model="form.notes" class="textarea" placeholder="Small stain on left cuff, measured 22in pit to pit" />
    </div>

    <template v-if="item">
      <template v-if="isOnHand(form.status)">
        <button class="btn btn-block" @click="sheet.open('sell', item)">Mark as sold</button>
        <button class="btn btn-block" @click="sheet.open('donate', item)">Mark as donated</button>
      </template>
      <button :class="['btn', 'btn-block', confirmDelete ? 'btn-danger' : 'btn-ghost']" @click="remove">
        {{ confirmDelete ? 'Tap again to delete permanently' : 'Delete item' }}
      </button>
    </template>

    <template #actions>
      <button class="btn" @click="sheet.close()">Cancel</button>
      <button class="btn btn-primary" :disabled="saving || busyPhoto" @click="save">
        {{ saving ? 'Saving…' : isNew ? 'Add item' : 'Save' }}
      </button>
    </template>
  </AppSheet>
</template>
