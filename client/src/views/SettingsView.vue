<script setup lang="ts">
import { computed, ref } from 'vue'
import { ApiError } from '@/api'
import { parseMoney, ZERO } from '@/domain/money'
import { PLATFORM_IDS, platformLabel, type FeeSchedule } from '@/domain/platforms'
import { useAuthStore } from '@/stores/auth'
import { useItemsStore } from '@/stores/items'
import { CURRENCIES, useSettingsStore } from '@/stores/settings'
import { useToastStore } from '@/stores/toast'

const settings = useSettingsStore()
const items = useItemsStore()
const auth = useAuthStore()
const toast = useToastStore()
const saving = ref(false)

async function commit(run: () => Promise<void>) {
  saving.value = true
  try {
    await run()
  } catch (error) {
    toast.show(error instanceof ApiError ? error.message : 'Could not save settings', 'error')
  } finally {
    saving.value = false
  }
}

const saveGeneral = (changes: Parameters<typeof settings.save>[0]) => commit(() => settings.save(changes))

function saveGoal(event: Event) {
  const cents = parseMoney((event.target as HTMLInputElement).value)
  void saveGeneral({ profitGoalCents: cents ?? ZERO })
}

function setFee(platform: string, key: keyof FeeSchedule, value: string | boolean) {
  const current = settings.fees[platform]
  if (!current) return
  const next: FeeSchedule = { ...current }
  if (key === 'includesShipping') next.includesShipping = value as boolean
  else next[key] = Number(value) || 0
  void commit(() => settings.saveFees({ ...settings.fees, [platform]: next }))
}

const withPhotos = computed(() => items.items.filter((i) => i.thumbnail).length)
const demoExpires = computed(() =>
  auth.me?.demoExpiresAt ? new Date(auth.me.demoExpiresAt).toLocaleString(undefined, { weekday: 'short', hour: 'numeric', minute: '2-digit' }) : '',
)
</script>

<template>
  <div class="card settings-group">
    <span class="section-label">General</span>

    <div class="field-row">
      <div class="field">
        <label for="currency">Currency</label>
        <select
          id="currency"
          class="select"
          :value="settings.general.currency"
          :disabled="saving"
          @change="saveGeneral({ currency: ($event.target as HTMLSelectElement).value })"
        >
          <option v-for="c in CURRENCIES" :key="c" :value="c">{{ c }}</option>
        </select>
      </div>
      <div class="field">
        <label for="theme">Theme</label>
        <select
          id="theme"
          class="select"
          :value="settings.general.theme"
          @change="saveGeneral({ theme: ($event.target as HTMLSelectElement).value as 'dark' | 'light' })"
        >
          <option value="dark">Dark</option>
          <option value="light">Light</option>
        </select>
      </div>
    </div>

    <div class="field">
      <label for="goal">Monthly profit goal</label>
      <input
        id="goal"
        class="input mono"
        type="number"
        inputmode="decimal"
        step="1"
        min="0"
        :value="settings.general.profitGoal || ''"
        placeholder="0 to hide"
        @blur="saveGoal"
      />
    </div>
  </div>

  <div class="card settings-group">
    <span class="section-label">Fee estimates</span>
    <p class="muted note">
      Rates vary by country, category and account, and they change. Check these against a recent payout — but a sale
      with its real payout entered never touches them.
    </p>

    <div class="fee-row">
      <span class="section-label">Platform</span>
      <span class="section-label">%</span>
      <span class="section-label">Fixed</span>
    </div>

    <div v-for="id in PLATFORM_IDS" :key="id">
      <div class="fee-row">
        <span class="fee-row-name">{{ platformLabel(id) }}</span>
        <div class="field">
          <input
            class="input mono"
            type="number"
            step="0.01"
            min="0"
            max="100"
            inputmode="decimal"
            :aria-label="`${platformLabel(id)} percent`"
            :value="settings.fees[id]?.percent"
            @blur="setFee(id, 'percent', ($event.target as HTMLInputElement).value)"
          />
        </div>
        <div class="field">
          <input
            class="input mono"
            type="number"
            step="0.01"
            min="0"
            inputmode="decimal"
            :aria-label="`${platformLabel(id)} fixed fee`"
            :value="settings.fees[id]?.fixed"
            @blur="setFee(id, 'fixed', ($event.target as HTMLInputElement).value)"
          />
        </div>
      </div>
      <label class="shipping-toggle">
        <input
          type="checkbox"
          :checked="settings.fees[id]?.includesShipping"
          @change="setFee(id, 'includesShipping', ($event.target as HTMLInputElement).checked)"
        />
        Fee also applies to the shipping the buyer paid
      </label>
    </div>

    <button class="btn btn-sm" :disabled="saving" @click="commit(settings.resetFees)">Reset to defaults</button>
  </div>

  <div class="card settings-group">
    <span class="section-label">Your data</span>
    <div class="toggle-row">
      <div class="toggle-row-text">
        <span class="toggle-row-title">Signed in as</span>
        <span class="toggle-row-sub">{{ auth.me?.isDemo ? `Demo account · deleted ${demoExpires}` : auth.me?.email }}</span>
      </div>
      <span class="mono dimmed count-note">{{ items.items.length }} items · {{ withPhotos }} with photos</span>
    </div>
    <p v-if="auth.me?.isDemo" class="muted note">
      This demo is yours alone and disappears after a day. Nothing here is shared with other visitors.
    </p>
  </div>

  <div class="card settings-group">
    <span class="section-label">About the numbers</span>
    <p class="muted note">
      Whenever a sale carries the payout you were actually paid, the platform's cut is worked out from that and these
      rates are ignored. They only stand in for a sale logged before the payout lands, and for the projected net on
      things still listed. Anything resting on an estimate is labelled “est.”
    </p>
    <p class="muted note">
      Profit is worked out on the server from exact decimal amounts; the sale form's live preview runs the same rules in
      your browser, checked against the same test cases.
    </p>
  </div>

  <p class="dimmed footer-note">{{ settings.general.displayName ? `${settings.general.displayName} · ` : '' }}Resell Tracker</p>
</template>

<style scoped>
.note { margin: 0; font-size: 13px; }
.shipping-toggle { display: flex; gap: 8px; align-items: center; font-size: 12.5px; color: var(--text-muted); }
.count-note { font-size: 12px; }
.footer-note { font-size: 12px; text-align: center; }
</style>
