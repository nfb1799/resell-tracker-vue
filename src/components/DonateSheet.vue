<script setup>
import { ref, computed } from 'vue'
import { useItems } from '../composables/useItems'
import { useToast } from '../composables/useToast'
import { vFocus } from '../composables/vFocus'
import { formatMoney, num } from '../lib/money'
import { getLocalDateString, daysListed } from '../lib/date'
import { statusAfterUndo } from '../lib/status'
import BottomSheet from './ui/BottomSheet.vue'
import BreakdownRow from './ui/BreakdownRow.vue'

// Retires an item that is never going to sell. No profit to work out — the whole
// point is that the cost does not come back — so this records where it went and
// writes the cost off.
const props = defineProps({ item: { type: Object, required: true } })
const emit = defineEmits(['close'])

const { items, updateItem, currency } = useItems()
const showToast = useToast()
const editing = props.item.status === 'donated'

const donation = ref({
  date: props.item.donation?.date || getLocalDateString(),
  org: props.item.donation?.org || '',
  receiptValue: props.item.donation?.receiptValue ?? '',
})
const saving = ref(false)

// Places already donated to, so the name stays spelled the same way.
const knownOrgs = computed(() => [...new Set(items.value.map(i => i.donation?.org).filter(Boolean))].sort())

const held = computed(() => daysListed(props.item))

async function handleSave() {
  saving.value = true
  const d = donation.value
  try {
    await updateItem(props.item.id, {
      status: 'donated',
      donation: {
        date: d.date,
        org: d.org.trim(),
        receiptValue: d.receiptValue === '' ? null : num(d.receiptValue),
      },
    })
    showToast('Marked as donated', 'success')
    emit('close')
  } catch (error) {
    console.error(error)
    showToast('Could not save the donation', 'error')
    saving.value = false
  }
}

async function handleUndo() {
  try {
    await updateItem(props.item.id, { status: statusAfterUndo(props.item), donation: null })
    showToast('Back in inventory', 'success')
    emit('close')
  } catch (error) {
    console.error(error)
    showToast('Could not undo the donation', 'error')
  }
}
</script>

<template>
  <BottomSheet :title="editing ? 'Edit donation' : 'Mark as donated'" @close="emit('close')">
    <div class="field-row">
      <div class="field">
        <label for="donate-date">Date donated</label>
        <input id="donate-date" v-model="donation.date" class="input" type="date" />
      </div>
      <div class="field">
        <label for="donate-receipt">Receipt value</label>
        <input id="donate-receipt" v-model="donation.receiptValue" class="input mono" type="number"
          inputmode="decimal" step="0.01" min="0" placeholder="if you got one" />
      </div>
    </div>

    <div class="field">
      <label for="donate-org">Donated to</label>
      <input id="donate-org" v-model="donation.org" v-focus class="input" list="known-orgs"
        placeholder="Goodwill on Jefferson" />
      <datalist id="known-orgs">
        <option v-for="o in knownOrgs" :key="o" :value="o" />
      </datalist>
    </div>

    <div class="card">
      <div class="breakdown">
        <BreakdownRow label="What you paid for it" :value="formatMoney(item.cost, currency)" />
        <BreakdownRow v-if="item.listPrice > 0" label="Was asking" :value="formatMoney(item.listPrice, currency)" />
        <BreakdownRow v-if="held !== null" label="Held for" :value="`${held} days`" />
        <BreakdownRow
          label="Written off"
          :value="`-${formatMoney(item.cost, currency)}`"
          :tone="num(item.cost) > 0 ? 'neg' : ''"
          total
        />
      </div>
    </div>

    <p class="muted" style="margin: 0; font-size: 12.5px">
      The cost is written off — counted separately from sale profit, not mixed into it.
      The receipt value is recorded as-is and not used in any calculation.
    </p>

    <button v-if="editing" class="btn btn-block btn-ghost" @click="handleUndo">
      Undo donation, put back in inventory
    </button>

    <template #actions>
      <button class="btn" @click="emit('close')">Cancel</button>
      <button class="btn btn-primary" :disabled="saving" @click="handleSave">
        {{ saving ? 'Saving…' : editing ? 'Save' : 'Donate' }}
      </button>
    </template>
  </BottomSheet>
</template>
