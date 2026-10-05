<script setup>
import { ref, computed } from 'vue'
import { useItems } from '../composables/useItems'
import { useToast } from '../composables/useToast'
import { vFocus } from '../composables/vFocus'
import { PLATFORM_IDS, platformLabel } from '../lib/platforms'
import { computeProfit, formatMoney, formatPercent, num, round2 } from '../lib/money'
import { getLocalDateString, daysListed } from '../lib/date'
import { statusAfterUndo } from '../lib/status'
import BottomSheet from './ui/BottomSheet.vue'
import BreakdownRow from './ui/BreakdownRow.vue'

// Records (or edits) the sale of one item. Four numbers, each one a thing you can
// actually read off a screen rather than work out:
//
//   listedFor  what it was up for — snapshotted here, so later edits to the
//              item's asking price cannot rewrite what happened
//   price      the offer you took: what the buyer paid for the item, before shipping
//   payout     what landed in your account, which fixes the platform's cut exactly
//   costs      the label, the packaging, what you paid for it in the first place
const props = defineProps({ item: { type: Object, required: true } })
const emit = defineEmits(['close', 'edit-details'])

const { updateItem, currency, feeSettings } = useItems()
const showToast = useToast()
const editing = props.item.status === 'sold'

const s = props.item.sale
const sale = ref({
  platform: s?.platform || props.item.platforms?.[0] || 'depop',
  listedFor: s?.listedFor ?? props.item.listPrice ?? '',
  price: s?.price ?? props.item.listPrice ?? '',
  payout: s?.payout ?? '',
  shippingCharged: s?.shippingCharged ?? '',
  shippingCost: s?.shippingCost ?? '',
  otherCosts: s?.otherCosts ?? '',
  date: s?.date || getLocalDateString(),
})
const saving = ref(false)

const preview = computed(() => computeProfit({ ...props.item, sale: sale.value }, feeSettings.value))
const held = computed(() => daysListed({ ...props.item, status: 'sold', sale: sale.value }))
// A payout above the total the buyer paid means one of the two was mistyped.
const payoutTooHigh = computed(() => !preview.value.feesEstimated && preview.value.fees < 0)

// How far under the asking price the accepted offer landed.
const listed = computed(() => num(sale.value.listedFor))
const accepted = computed(() => num(sale.value.price))
const discount = computed(() => (listed.value > 0 && accepted.value > 0 && accepted.value < listed.value
  ? { amount: round2(listed.value - accepted.value), fraction: (listed.value - accepted.value) / listed.value }
  : null))

async function handleSave() {
  if (accepted.value <= 0) {
    showToast('Enter the offer you accepted', 'error')
    return
  }
  saving.value = true
  const v = sale.value
  try {
    await updateItem(props.item.id, {
      status: 'sold',
      sale: {
        platform: v.platform,
        listedFor: num(v.listedFor),
        price: accepted.value,
        // Left blank, the payout stays null and the platform's cut falls back
        // to the estimated rate until you fill in the real figure.
        payout: v.payout === '' ? null : num(v.payout),
        shippingCharged: num(v.shippingCharged),
        shippingCost: num(v.shippingCost),
        otherCosts: num(v.otherCosts),
        date: v.date,
      },
    })
    showToast(editing ? 'Sale updated' : `Sold for ${formatMoney(preview.value.net, currency.value)} net`, 'success')
    emit('close')
  } catch (error) {
    console.error(error)
    showToast('Could not save the sale', 'error')
    saving.value = false
  }
}

async function handleUnsell() {
  try {
    await updateItem(props.item.id, { status: statusAfterUndo(props.item), sale: null })
    showToast('Moved back to inventory', 'success')
    emit('close')
  } catch (error) {
    console.error(error)
    showToast('Could not undo the sale', 'error')
  }
}
</script>

<template>
  <BottomSheet :title="editing ? 'Edit sale' : 'Log a sale'" @close="emit('close')">
    <div class="field">
      <label>Platform</label>
      <div class="chip-row">
        <button v-for="id in PLATFORM_IDS" :key="id" type="button"
          :class="['chip', { active: sale.platform === id }]" @click="sale.platform = id">
          {{ platformLabel(id) }}
        </button>
      </div>
    </div>

    <div class="field-row">
      <div class="field">
        <label for="sale-listed">Listed for</label>
        <input id="sale-listed" v-model="sale.listedFor" class="input mono" type="number" inputmode="decimal"
          step="0.01" min="0" placeholder="0.00" />
      </div>
      <div class="field">
        <label for="sale-price">Offer accepted</label>
        <input id="sale-price" v-model="sale.price" v-focus class="input mono" type="number" inputmode="decimal"
          step="0.01" min="0" />
      </div>
    </div>

    <p class="field-hint">
      Offer accepted is what the buyer paid for the item, before shipping. Sold at
      full price? Leave it matching what you listed for.
    </p>

    <div class="field-row">
      <div class="field">
        <label for="sale-payout">You got paid</label>
        <input id="sale-payout" v-model="sale.payout" class="input mono" type="number" inputmode="decimal"
          step="0.01" min="0" placeholder="from payout" />
      </div>
      <div class="field">
        <label for="sale-date">Sale date</label>
        <input id="sale-date" v-model="sale.date" class="input" type="date" />
      </div>
    </div>

    <div class="field-row">
      <div class="field">
        <label for="ship-charged">Shipping buyer paid</label>
        <input id="ship-charged" v-model="sale.shippingCharged" class="input mono" type="number" inputmode="decimal"
          step="0.01" min="0" placeholder="0.00" />
      </div>
      <div class="field">
        <label for="ship-cost">Shipping you paid</label>
        <input id="ship-cost" v-model="sale.shippingCost" class="input mono" type="number" inputmode="decimal"
          step="0.01" min="0" placeholder="0.00" />
      </div>
    </div>

    <div class="field">
      <label for="other-costs">Other costs</label>
      <input id="other-costs" v-model="sale.otherCosts" class="input mono" type="number" inputmode="decimal"
        step="0.01" min="0" placeholder="Packaging, tape" />
    </div>

    <div class="card">
      <div class="breakdown">
        <BreakdownRow v-if="listed > 0" label="Listed for" :value="formatMoney(listed, currency)" />
        <BreakdownRow label="Offer accepted" :value="formatMoney(accepted, currency)" />
        <BreakdownRow v-if="discount" label="Came down by"
          :value="`${formatMoney(discount.amount, currency)} · ${formatPercent(discount.fraction)}`" />
        <BreakdownRow v-if="num(sale.shippingCharged) > 0" label="Shipping collected"
          :value="formatMoney(sale.shippingCharged, currency)" />
        <BreakdownRow
          :label="`${platformLabel(sale.platform)} kept${preview.feesEstimated ? ' (est.)' : ''}`"
          :value="`-${formatMoney(preview.fees, currency)}`"
        />
        <BreakdownRow
          :label="preview.feesEstimated ? 'You got paid (est.)' : 'You got paid'"
          :value="formatMoney(preview.payout, currency)"
        />
        <BreakdownRow label="Cost of goods" :value="`-${formatMoney(preview.cogs, currency)}`" />
        <BreakdownRow v-if="preview.shippingCost > 0" label="Shipping paid"
          :value="`-${formatMoney(preview.shippingCost, currency)}`" />
        <BreakdownRow v-if="preview.otherCosts > 0" label="Other costs"
          :value="`-${formatMoney(preview.otherCosts, currency)}`" />
        <BreakdownRow
          label="Net profit"
          :value="formatMoney(preview.net, currency)"
          :tone="preview.net >= 0 ? 'pos' : 'neg'"
          total
        />
        <BreakdownRow
          label="Margin · ROI · days held"
          :value="[
            formatPercent(preview.margin),
            preview.roi === null ? '—' : formatPercent(preview.roi),
            held === null ? '—' : `${held}d`,
          ].join('  ·  ')"
        />
      </div>
    </div>

    <p v-if="payoutTooHigh" class="inline-warning">
      The payout is more than the buyer paid. Check both figures — shipping the
      buyer covered belongs in “shipping buyer paid”.
    </p>

    <!-- A sold item opens straight into its sale, so this is the only way back
         to its title, cost and photo. -->
    <button class="btn btn-block" @click="emit('edit-details', item)">
      Edit item details
    </button>

    <button v-if="editing" class="btn btn-block btn-ghost" @click="handleUnsell">
      Undo sale, put back in inventory
    </button>

    <template #actions>
      <button class="btn" @click="emit('close')">Cancel</button>
      <button class="btn btn-primary" :disabled="saving" @click="handleSave">
        {{ saving ? 'Saving…' : editing ? 'Save sale' : 'Mark sold' }}
      </button>
    </template>
  </BottomSheet>
</template>
