<script setup>
import { ref, computed, onMounted, toRaw } from 'vue'
import { useItems } from '../composables/useItems'
import { useToast } from '../composables/useToast'
import { vFocus } from '../composables/vFocus'
import { PLATFORM_IDS, platformLabel } from '../lib/platforms'
import { formatMoney, projectedNet, num } from '../lib/money'
import { getLocalDateString } from '../lib/date'
import { processPhoto } from '../lib/image'
import { CONDITIONS, blankItem } from '../lib/itemFields'
import BottomSheet from './ui/BottomSheet.vue'
import BreakdownRow from './ui/BreakdownRow.vue'

const props = defineProps({ item: { type: Object, default: null } })
const emit = defineEmits(['close', 'sell', 'donate'])

const { items, addItem, updateItem, deleteItem, setPhoto, removePhoto, getPhoto, currency, feeSettings } = useItems()
const showToast = useToast()
const isNew = !props.item?.id

const form = ref(isNew
  ? blankItem()
  : { ...blankItem(), ...props.item, platforms: [...(props.item.platforms || [])] })
const saving = ref(false)
const confirmDelete = ref(false)
// The thumbnail is enough to show straight away; the full photo replaces it
// once its own document loads.
const preview = ref(props.item?.thumb || '')
const pendingPhoto = ref(null)
const photoCleared = ref(false)
const busyPhoto = ref(false)
const fileInput = ref(null)

onMounted(() => {
  if (isNew || !props.item?.thumb) return
  getPhoto(props.item.id)
    .then(full => { if (full && !pendingPhoto.value && !photoCleared.value) preview.value = full })
    .catch(error => console.error('Could not load photo:', error))
})

function togglePlatform(id) {
  const prev = form.value
  const platforms = prev.platforms.includes(id)
    ? prev.platforms.filter(p => p !== id)
    : [...prev.platforms, id]
  // First platform added implies the item is now live somewhere.
  const becameListed = platforms.length > 0 && prev.status === 'inventory'
  form.value = {
    ...prev,
    platforms,
    status: becameListed ? 'listed' : platforms.length === 0 && prev.status === 'listed' ? 'inventory' : prev.status,
    listedDate: becameListed && !prev.listedDate ? getLocalDateString() : prev.listedDate,
  }
}

async function handlePickPhoto(e) {
  const file = e.target.files?.[0]
  e.target.value = '' // so picking the same file twice still fires a change
  if (!file) return
  busyPhoto.value = true
  try {
    const photo = await processPhoto(file)
    pendingPhoto.value = photo
    preview.value = photo.full
    photoCleared.value = false
  } catch (error) {
    console.error(error)
    showToast(error.message || 'Could not read that image', 'error')
  } finally {
    busyPhoto.value = false
  }
}

function handleClearPhoto() {
  pendingPhoto.value = null
  preview.value = ''
  photoCleared.value = true
}

// Suggest categories and sources already used, so they stay consistent.
const knownCategories = computed(() => [...new Set(items.value.map(i => i.category).filter(Boolean))].sort())
const knownSources = computed(() => [...new Set(items.value.map(i => i.source).filter(Boolean))].sort())

const projected = computed(() => projectedNet(form.value, feeSettings.value))

async function handleSave() {
  if (!form.value.title.trim()) {
    showToast('Give the item a title', 'error')
    return
  }
  saving.value = true
  try {
    const payload = {
      ...toRaw(form.value),
      platforms: [...form.value.platforms],
      title: form.value.title.trim(),
      cost: num(form.value.cost),
      listPrice: num(form.value.listPrice),
    }
    // The photo helpers own `thumb`; keep the form from writing a stale copy.
    delete payload.thumb
    delete payload.id

    let itemId
    if (isNew) {
      itemId = await addItem(payload)
    } else {
      await updateItem(props.item.id, payload)
      itemId = props.item.id
    }

    if (pendingPhoto.value) {
      await setPhoto(itemId, pendingPhoto.value)
    } else if (photoCleared.value && !isNew) {
      await removePhoto(itemId)
    }

    if (!isNew) showToast('Saved', 'success')
    emit('close')
  } catch (error) {
    console.error(error)
    showToast('Could not save item', 'error')
    saving.value = false
  }
}

async function handleDelete() {
  if (!confirmDelete.value) {
    confirmDelete.value = true
    return
  }
  try {
    await deleteItem(props.item.id)
    emit('close')
  } catch (error) {
    console.error(error)
    showToast('Could not delete item', 'error')
  }
}
</script>

<template>
  <BottomSheet :title="isNew ? 'New item' : form.title || 'Edit item'" @close="emit('close')">
    <div class="photo-field">
      <div class="photo-frame">
        <img v-if="preview" :src="preview" :alt="form.title || 'Item photo'" />
        <span v-else class="photo-empty">No photo</span>
      </div>
      <div class="photo-actions">
        <input ref="fileInput" type="file" accept="image/*" capture="environment" hidden @change="handlePickPhoto" />
        <button class="btn btn-sm" :disabled="busyPhoto" @click="fileInput?.click()">
          {{ busyPhoto ? 'Working…' : preview ? 'Replace photo' : 'Add photo' }}
        </button>
        <button v-if="preview" class="btn btn-sm btn-ghost" :disabled="busyPhoto" @click="handleClearPhoto">
          Remove
        </button>
      </div>
    </div>

    <div class="field">
      <label for="title">Title</label>
      <input id="title" v-model="form.title" v-focus="isNew" class="input" placeholder="Carhartt detroit jacket" />
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
        <input id="cost" v-model="form.cost" class="input mono" type="number" inputmode="decimal" step="0.01" min="0"
          placeholder="0.00" />
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
      <button v-for="id in PLATFORM_IDS" :key="id" type="button"
        :class="['chip', { active: form.platforms.includes(id) }]" @click="togglePlatform(id)">
        {{ platformLabel(id) }}
      </button>
    </div>

    <div class="field-row">
      <div class="field">
        <label for="listPrice">Asking price</label>
        <input id="listPrice" v-model="form.listPrice" class="input mono" type="number" inputmode="decimal"
          step="0.01" min="0" placeholder="0.00" />
      </div>
      <div class="field">
        <label for="listedDate">Listed on</label>
        <input id="listedDate" v-model="form.listedDate" class="input" type="date" />
      </div>
    </div>

    <div v-if="projected !== null" class="breakdown">
      <BreakdownRow
        :label="`Est. net if it sells at asking on ${platformLabel(form.platforms[0] || 'other')}`"
        :value="formatMoney(projected, currency)"
        :tone="projected >= 0 ? 'pos' : 'neg'"
      />
    </div>

    <div class="field">
      <label for="notes">Notes</label>
      <textarea id="notes" v-model="form.notes" class="textarea"
        placeholder="Small stain on left cuff, measured 22in pit to pit" />
    </div>

    <template v-if="!isNew">
      <template v-if="form.status !== 'sold' && form.status !== 'donated'">
        <button class="btn btn-block" @click="emit('sell', item)">Mark as sold</button>
        <button class="btn btn-block" @click="emit('donate', item)">Mark as donated</button>
      </template>
      <button :class="['btn', 'btn-block', confirmDelete ? 'btn-danger' : 'btn-ghost']" @click="handleDelete">
        {{ confirmDelete ? 'Tap again to delete permanently' : 'Delete item' }}
      </button>
    </template>

    <template #actions>
      <button class="btn" @click="emit('close')">Cancel</button>
      <button class="btn btn-primary" :disabled="saving || busyPhoto" @click="handleSave">
        {{ saving ? 'Saving…' : isNew ? 'Add item' : 'Save' }}
      </button>
    </template>
  </BottomSheet>
</template>
