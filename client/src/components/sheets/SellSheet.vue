<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { ApiError } from '@/api'
import { daysListed, getLocalDateString } from '@/domain/dates'
import type { Item } from '@/domain/item'
import { formatMoney, formatPercent, parseMoney, toDollars, ZERO, type Cents } from '@/domain/money'
import { PLATFORM_IDS, platformLabel } from '@/domain/platforms'
import { computeProfit, markdown, type SaleFigures } from '@/domain/profit'
import { useItemsStore } from '@/stores/items'
import { useSettingsStore } from '@/stores/settings'
import { useSheetStore } from '@/stores/sheet'
import { useToastStore } from '@/stores/toast'
import AppSheet from '../AppSheet.vue'
import BreakdownRow from '../BreakdownRow.vue'

// Logs or edits the sale of one item. The breakdown updates as you type, from the
// client's mirror of the profit math; what is saved, and shown everywhere after,
// is the server's figure. Both run the same shared fixture, so they agree.
//
//   listed for  what it was up for, snapshotted so later edits to the item's
//               asking price cannot rewrite what happened
//   accepted    the offer taken: what the buyer paid for the item, before shipping
//   payout      what landed in your account, which fixes the platform's cut exactly
const props = defineProps<{ item: Item }>()

const items = useItemsStore()
const settings = useSettingsStore()
const sheet = useSheetStore()
const toast = useToastStore()

const editing = props.item.status === 'sold'
const asInput = (c: Cents | null | undefined) => (c === null || c === undefined ? '' : String(toDollars(c)))
const existing = props.item.sale

const form = reactive({
  platform: existing?.platform ?? props.item.platforms[0] ?? 'depop',
  listedFor: asInput(existing?.listedFor ?? props.item.listPrice),
  price: asInput(existing?.price ?? props.item.listPrice),
  payout: asInput(existing?.payout),
  shippingCharged: asInput(existing?.shippingCharged),
  shippingCost: asInput(existing?.shippingCost),
  otherCosts: asInput(existing?.otherCosts),
  date: existing?.date ?? getLocalDateString(),
})
const saving = ref(false)

const money = (value: string): Cents => parseMoney(value) ?? ZERO

const figures = computed<SaleFigures>(() => ({
  platform: form.platform,
  price: money(form.price),
  shippingCharged: money(form.shippingCharged),
  // Left blank, the payout stays unknown and the platform's cut is estimated.
  payout: parseMoney(form.payout),
  shippingCost: money(form.shippingCost),
  otherCosts: money(form.otherCosts),
}))

const preview = computed(() => computeProfit(props.item.cost, figures.value, settings.fees))
const listed = computed(() => money(form.listedFor))
const discount = computed(() => markdown(listed.value, figures.value.price))
const held = computed(() =>
  daysListed({ status: 'sold', acquiredDate: props.item.acquiredDate, listedDate: props.item.listedDate, saleDate: form.date }),
)
// A payout above the total the buyer paid means one of the two was mistyped.
const payoutTooHigh = computed(() => !preview.value.feesEstimated && preview.value.fees < 0)

const currency = computed(() => settings.currency)
const fmt = (c: Cents) => formatMoney(c, currency.value)

async function save() {
  if (figures.value.price <= 0) {
    toast.show('Enter the offer you accepted', 'error')
    return
  }
  saving.value = true
  try {
    const sold = await items.sell(props.item, { ...figures.value, listedFor: listed.value, date: form.date })
    toast.show(editing ? 'Sale updated' : `Sold for ${fmt(sold.profit?.net ?? ZERO)} net`, 'success')
    sheet.close()
  } catch (error) {
    if (!(error instanceof ApiError && error.isStale)) toast.show(error instanceof ApiError ? error.message : 'Could not save the sale', 'error')
    saving.value = false
  }
}

async function undo() {
  try {
    await items.undoSale(props.item)
    toast.show('Moved back to inventory', 'success')
    sheet.close()
  } catch (error) {
    if (!(error instanceof ApiError && error.isStale)) toast.show('Could not undo the sale', 'error')
  }
}
</script>

<template>
  <AppSheet :title="editing ? 'Edit sale' : 'Log a sale'" @close="sheet.close()">
    <div class="field">
      <label>Platform</label>
      <div class="chip-row">
        <button
          v-for="id in PLATFORM_IDS"
          :key="id"
          type="button"
          :class="['chip', { active: form.platform === id }]"
          :aria-pressed="form.platform === id"
          @click="form.platform = id"
        >
          {{ platformLabel(id) }}
        </button>
      </div>
    </div>

    <div class="field-row">
      <div class="field">
        <label for="sale-listed">Listed for</label>
        <input id="sale-listed" v-model="form.listedFor" class="input mono" type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" />
      </div>
      <div class="field">
        <label for="sale-price">Offer accepted</label>
        <input id="sale-price" v-model="form.price" class="input mono" type="number" inputmode="decimal" step="0.01" min="0" autofocus />
      </div>
    </div>

    <p class="field-hint">
      Offer accepted is what the buyer paid for the item, before shipping. Sold at full price? Leave it matching what you
      listed for.
    </p>

    <div class="field-row">
      <div class="field">
        <label for="sale-payout">You got paid</label>
        <input id="sale-payout" v-model="form.payout" class="input mono" type="number" inputmode="decimal" step="0.01" min="0" placeholder="from payout" />
      </div>
      <div class="field">
        <label for="sale-date">Sale date</label>
        <input id="sale-date" v-model="form.date" class="input" type="date" />
      </div>
    </div>

    <div class="field-row">
      <div class="field">
        <label for="ship-charged">Shipping buyer paid</label>
        <input id="ship-charged" v-model="form.shippingCharged" class="input mono" type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" />
      </div>
      <div class="field">
        <label for="ship-cost">Shipping you paid</label>
        <input id="ship-cost" v-model="form.shippingCost" class="input mono" type="number" inputmode="decimal" step="0.01" min="0" placeholder="0.00" />
      </div>
    </div>

    <div class="field">
      <label for="other-costs">Other costs</label>
      <input id="other-costs" v-model="form.otherCosts" class="input mono" type="number" inputmode="decimal" step="0.01" min="0" placeholder="Packaging, tape" />
    </div>

    <div class="card">
      <div class="breakdown" aria-live="polite">
        <BreakdownRow v-if="listed > 0" label="Listed for" :value="fmt(listed)" />
        <BreakdownRow label="Offer accepted" :value="fmt(figures.price)" />
        <BreakdownRow v-if="discount" label="Came down by" :value="`${fmt(discount.amount)} · ${formatPercent(discount.fraction)}`" />
        <BreakdownRow v-if="figures.shippingCharged > 0" label="Shipping collected" :value="fmt(figures.shippingCharged)" />
        <BreakdownRow :label="`${platformLabel(form.platform)} kept${preview.feesEstimated ? ' (est.)' : ''}`" :value="`-${fmt(preview.fees)}`" />
        <BreakdownRow :label="preview.feesEstimated ? 'You got paid (est.)' : 'You got paid'" :value="fmt(preview.payout)" />
        <BreakdownRow label="Cost of goods" :value="`-${fmt(preview.cogs)}`" />
        <BreakdownRow v-if="preview.shippingCost > 0" label="Shipping paid" :value="`-${fmt(preview.shippingCost)}`" />
        <BreakdownRow v-if="preview.otherCosts > 0" label="Other costs" :value="`-${fmt(preview.otherCosts)}`" />
        <BreakdownRow label="Net profit" :value="fmt(preview.net)" :tone="preview.net >= 0 ? 'pos' : 'neg'" total />
        <BreakdownRow
          label="Margin · ROI · days held"
          :value="[formatPercent(preview.margin), preview.roi === null ? '—' : formatPercent(preview.roi), held === null ? '—' : `${held}d`].join('  ·  ')"
        />
      </div>
    </div>

    <p v-if="payoutTooHigh" class="inline-warning">
      The payout is more than the buyer paid. Check both figures — shipping the buyer covered belongs in “shipping buyer
      paid”.
    </p>

    <!-- A sold item opens straight into its sale, so this is the way back to its title, cost and photo. -->
    <button class="btn btn-block" @click="sheet.open('edit', item)">Edit item details</button>
    <button v-if="editing" class="btn btn-block btn-ghost" @click="undo">Undo sale, put back in inventory</button>

    <template #actions>
      <button class="btn" @click="sheet.close()">Cancel</button>
      <button class="btn btn-primary" :disabled="saving" @click="save">
        {{ saving ? 'Saving…' : editing ? 'Save sale' : 'Mark sold' }}
      </button>
    </template>
  </AppSheet>
</template>
