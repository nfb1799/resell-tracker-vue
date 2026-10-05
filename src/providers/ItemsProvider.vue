<script setup>
import { computed, provide, shallowRef, watch } from 'vue'
import { useAuth } from '../composables/useAuth'
import { useToast } from '../composables/useToast'
import { ItemsKey } from '../composables/keys'
import * as service from '../firebase/firestoreService'
import { withFeeDefaults } from '../lib/platforms'

// Single source of truth for inventory. Every screen reads the same live list
// rather than fetching its own copy, so a sale logged on a phone shows up on a
// laptop without a refresh.
const { currentUser, userProfile, updateUserProfile } = useAuth()
const showToast = useToast()

// The snapshot carries the uid it belongs to, so switching accounts reads as
// "loading" until the new account's first snapshot lands. Shallow on purpose:
// documents are replaced wholesale on every snapshot, never mutated in place.
const snapshot = shallowRef({ userId: null, items: [] })
const userId = computed(() => currentUser.value?.uid)

watch(userId, (id, _previous, onCleanup) => {
  if (!id) return
  const unsubscribe = service.subscribeItems(
    id,
    (docs) => { snapshot.value = { userId: id, items: docs } },
    () => {
      snapshot.value = { userId: id, items: [] }
      showToast('Could not load inventory', 'error')
    }
  )
  onCleanup(unsubscribe)
}, { immediate: true })

const loading = computed(() => snapshot.value.userId !== userId.value)
const items = computed(() => (loading.value ? [] : snapshot.value.items))

const settings = computed(() => userProfile.value?.settings || {})
const feeSettings = computed(() => withFeeDefaults(settings.value.fees))
const currency = computed(() => settings.value.currency || 'USD')

// `notify: false` is for bulk import, which reports one summary at the end
// rather than a toast per row. The write itself is the same either way.
async function addItem(item, { notify = true } = {}) {
  const id = await service.addItem(userId.value, item)
  if (notify) showToast('Item added', 'success')
  return id
}

async function updateItem(itemId, updates) {
  await service.updateItem(userId.value, itemId, updates)
}

async function deleteItem(itemId) {
  await service.deleteItem(userId.value, itemId)
  showToast('Item deleted', 'success')
}

// Photos: the thumbnail rides on the item document, the full-size JPEG goes
// in its own document. Both move together so they can never disagree.
async function setPhoto(itemId, { thumb, full }) {
  await service.setItemPhoto(userId.value, itemId, full)
  await service.updateItem(userId.value, itemId, { thumb })
}

async function removePhoto(itemId) {
  await service.deleteItemPhoto(userId.value, itemId)
  await service.updateItem(userId.value, itemId, { thumb: '' })
}

const getPhoto = (itemId) => service.getItemPhoto(userId.value, itemId)

async function saveSettings(updates) {
  await updateUserProfile({ settings: { ...settings.value, ...updates } })
}

provide(ItemsKey, {
  items,
  loading,
  settings,
  feeSettings,
  currency,
  addItem,
  updateItem,
  deleteItem,
  setPhoto,
  removePhoto,
  getPhoto,
  saveSettings,
})
</script>

<template>
  <slot />
</template>
