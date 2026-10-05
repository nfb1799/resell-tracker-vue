<script setup>
import { ref, computed } from 'vue'
import { useItems } from '../composables/useItems'
import { useAuth } from '../composables/useAuth'
import { useToast } from '../composables/useToast'
import { PLATFORM_IDS, platformLabel, defaultFeeSettings } from '../lib/platforms'
import { num } from '../lib/money'
import { itemsToCsv, downloadCsv } from '../lib/csv'
import { getLocalDateString } from '../lib/date'

const CURRENCIES = ['USD', 'GBP', 'EUR', 'CAD', 'AUD']

const { items, settings, feeSettings, saveSettings, currency } = useItems()
const { currentUser, userProfile } = useAuth()
const showToast = useToast()
const saving = ref(false)

async function commit(updates) {
  saving.value = true
  try {
    await saveSettings(updates)
  } catch (error) {
    console.error(error)
    showToast('Could not save settings', 'error')
  } finally {
    saving.value = false
  }
}

function setFee(platform, key, value) {
  const fees = feeSettings.value
  commit({
    fees: {
      ...fees,
      [platform]: { ...fees[platform], [key]: key === 'includesShipping' ? value : num(value) },
    },
  })
}

function setTheme(theme) {
  document.documentElement.setAttribute('data-theme', theme)
  localStorage.setItem('theme', theme)
  commit({ theme })
}

function exportAll() {
  downloadCsv(`inventory-${getLocalDateString()}.csv`, itemsToCsv(items.value, feeSettings.value))
}

function exportJson() {
  // Thumbnails would bloat the backup badly; the full photos live in their own
  // documents and are not included either.
  const bare = items.value.map(item => {
    const copy = { ...item }
    delete copy.thumb
    return copy
  })
  const blob = new Blob(
    [JSON.stringify({ exportedAt: new Date().toISOString(), settings: settings.value, items: bare }, null, 2)],
    { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `resell-backup-${getLocalDateString()}.json`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}

const theme = computed(() => settings.value.theme || localStorage.getItem('theme') || 'dark')
const withPhotos = computed(() => items.value.filter(i => i.thumb).length)
</script>

<template>
  <div class="card settings-group">
    <span class="section-label">General</span>

    <div class="field-row">
      <div class="field">
        <label for="currency">Currency</label>
        <select id="currency" class="select" :value="currency" :disabled="saving"
          @change="commit({ currency: $event.target.value })">
          <option v-for="c in CURRENCIES" :key="c" :value="c">{{ c }}</option>
        </select>
      </div>
      <div class="field">
        <label for="theme">Theme</label>
        <select id="theme" class="select" :value="theme" @change="setTheme($event.target.value)">
          <option value="dark">Dark</option>
          <option value="light">Light</option>
        </select>
      </div>
    </div>

    <div class="field">
      <label for="goal">Monthly profit goal</label>
      <input id="goal" class="input mono" type="number" inputmode="decimal" step="1" min="0"
        :value="settings.profitGoal || ''" placeholder="0 to hide"
        @blur="commit({ profitGoal: num($event.target.value) })" />
    </div>
  </div>

  <div class="card settings-group">
    <span class="section-label">Fee estimates</span>
    <p class="muted" style="margin: 0; font-size: 13px">
      Rates vary by country, category and account, and they change. Check these
      against a recent payout — but a sale with its real payout entered never
      touches them.
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
          <input class="input mono" type="number" step="0.01" min="0" inputmode="decimal"
            :value="feeSettings[id].percent" :aria-label="`${platformLabel(id)} percent fee`"
            @blur="setFee(id, 'percent', $event.target.value)" />
        </div>
        <div class="field">
          <input class="input mono" type="number" step="0.01" min="0" inputmode="decimal"
            :value="feeSettings[id].fixed" :aria-label="`${platformLabel(id)} fixed fee`"
            @blur="setFee(id, 'fixed', $event.target.value)" />
        </div>
      </div>
      <label style="display: flex; gap: 8px; align-items: center; font-size: 12.5px; color: var(--text-muted)">
        <input type="checkbox" :checked="!!feeSettings[id].includesShipping"
          @change="setFee(id, 'includesShipping', $event.target.checked)" />
        Fee also applies to the shipping the buyer paid
      </label>
    </div>

    <button class="btn btn-sm" :disabled="saving" @click="commit({ fees: defaultFeeSettings() })">
      Reset to defaults
    </button>
  </div>

  <div class="card settings-group">
    <span class="section-label">Your data</span>
    <div class="toggle-row">
      <div class="toggle-row-text">
        <span class="toggle-row-title">Export CSV</span>
        <span class="toggle-row-sub">Every item with its profit breakdown</span>
      </div>
      <button class="btn btn-sm" @click="exportAll">Export</button>
    </div>
    <div class="toggle-row">
      <div class="toggle-row-text">
        <span class="toggle-row-title">Download backup</span>
        <span class="toggle-row-sub">Raw JSON of items and settings, photos excluded</span>
      </div>
      <button class="btn btn-sm" @click="exportJson">Backup</button>
    </div>
    <div class="toggle-row">
      <div class="toggle-row-text">
        <span class="toggle-row-title">Signed in as</span>
        <span class="toggle-row-sub">
          {{ currentUser?.email || (currentUser?.isAnonymous ? 'Guest account on this device' : '—') }}
        </span>
      </div>
      <span class="mono dimmed" style="font-size: 12px">
        {{ items.length }} items · {{ withPhotos }} with photos
      </span>
    </div>
    <p v-if="currentUser?.isAnonymous" class="muted" style="margin: 0; font-size: 12.5px">
      Guest data lives under an anonymous account. Sign out without a backup and it is
      gone — download the JSON first, or make a real account.
    </p>
  </div>

  <div class="card settings-group">
    <span class="section-label">About the numbers</span>
    <p class="muted" style="margin: 0; font-size: 13px">
      Whenever a sale carries the payout you were actually paid, the platform's cut
      is worked out from that and these rates are ignored. They only stand in for a
      sale logged before the payout lands, and for the projected net on things still
      listed. Anything resting on an estimate is labelled “est.”
    </p>
    <p class="muted" style="margin: 0; font-size: 13px">
      Photos are shrunk in your browser and stored in Firestore rather than Cloud
      Storage, which keeps the project on the no-card free tier. One photo per item.
    </p>
  </div>

  <p class="dimmed" style="font-size: 12px; text-align: center">
    {{ userProfile?.displayName ? `${userProfile.displayName} · ` : '' }}Resell Tracker
  </p>
</template>
