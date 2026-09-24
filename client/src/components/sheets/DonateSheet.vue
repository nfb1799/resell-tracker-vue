<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { ApiError } from '@/api'
import { getLocalDateString } from '@/domain/dates'
import type { Item } from '@/domain/item'
import { formatMoney, parseMoney, toDollars } from '@/domain/money'
import { itemDaysListed } from '@/lib/format'
import { useItemsStore } from '@/stores/items'
import { useSettingsStore } from '@/stores/settings'
import { useSheetStore } from '@/stores/sheet'
import { useToastStore } from '@/stores/toast'
import AppSheet from '../AppSheet.vue'
import BreakdownRow from '../BreakdownRow.vue'

// Retires an item that is never going to sell. There is no profit to work out
// (the point is that the cost does not come back), so this records where it went
// and writes the cost off.
const props = defineProps<{ item: Item }>()

const items = useItemsStore()
const settings = useSettingsStore()
const sheet = useSheetStore()
const toast = useToastStore()

const editing = props.item.status === 'donated'
const form = reactive({
  date: props.item.donation?.date ?? getLocalDateString(),
  org: props.item.donation?.org ?? '',
  receiptValue: props.item.donation?.receiptValue == null ? '' : String(toDollars(props.item.donation.receiptValue)),
})
const saving = ref(false)

// Places already donated to, so the name stays spelled the same way.
const knownOrgs = computed(() => [...new Set(items.items.map((i) => i.donation?.org).filter((o): o is string => !!o))].sort())
const held = computed(() => itemDaysListed(props.item))
const fmt = (c: Parameters<typeof formatMoney>[0]) => formatMoney(c, settings.currency)

async function save() {
  saving.value = true
  try {
    // The receipt value is stored as typed and used in no calculation.
    await items.donate(props.item, { date: form.date, org: form.org.trim(), receiptValue: parseMoney(form.receiptValue) })
    toast.show(editing ? 'Donation updated' : 'Marked as donated', 'success')
    sheet.close()
  } catch (error) {
    if (!(error instanceof ApiError && error.isStale)) toast.show(error instanceof ApiError ? error.message : 'Could not save the donation', 'error')
    saving.value = false
  }
}

async function undo() {
  try {
    await items.undoDonation(props.item)
    toast.show('Back in inventory', 'success')
    sheet.close()
  } catch (error) {
    if (!(error instanceof ApiError && error.isStale)) toast.show('Could not undo the donation', 'error')
  }
}
</script>

<template>
  <AppSheet :title="editing ? 'Edit donation' : 'Mark as donated'" @close="sheet.close()">
    <div class="field-row">
      <div class="field">
        <label for="donate-date">Date donated</label>
        <input id="donate-date" v-model="form.date" class="input" type="date" />
      </div>
      <div class="field">
        <label for="donate-receipt">Receipt value</label>
        <input id="donate-receipt" v-model="form.receiptValue" class="input mono" type="number" inputmode="decimal" step="0.01" min="0" placeholder="if you got one" />
      </div>
    </div>

    <div class="field">
      <label for="donate-org">Donated to</label>
      <input id="donate-org" v-model="form.org" class="input" list="known-orgs" placeholder="Goodwill on Jefferson" autofocus />
      <datalist id="known-orgs">
        <option v-for="o in knownOrgs" :key="o" :value="o" />
      </datalist>
    </div>

    <div class="card">
      <div class="breakdown">
        <BreakdownRow label="What you paid for it" :value="fmt(item.cost)" />
        <BreakdownRow v-if="item.listPrice" label="Was asking" :value="fmt(item.listPrice)" />
        <BreakdownRow v-if="held !== null" label="Held for" :value="`${held} days`" />
        <BreakdownRow label="Written off" :value="`-${fmt(item.cost)}`" :tone="item.cost > 0 ? 'neg' : undefined" total />
      </div>
    </div>

    <p class="muted donate-note">
      The cost is written off — counted separately from sale profit, not mixed into it. The receipt value is recorded
      as-is and not used in any calculation.
    </p>

    <button v-if="editing" class="btn btn-block btn-ghost" @click="undo">Undo donation, put back in inventory</button>

    <template #actions>
      <button class="btn" @click="sheet.close()">Cancel</button>
      <button class="btn btn-primary" :disabled="saving" @click="save">
        {{ saving ? 'Saving…' : editing ? 'Save' : 'Donate' }}
      </button>
    </template>
  </AppSheet>
</template>

<style scoped>
.donate-note { margin: 0; font-size: 12.5px; }
</style>
