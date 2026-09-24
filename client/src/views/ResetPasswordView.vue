<script setup lang="ts">
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { ApiError, authApi } from '@/api'

// Where the emailed reset link lands: ?email=…&token=…
const route = useRoute()
const email = typeof route.query.email === 'string' ? route.query.email : ''
const token = typeof route.query.token === 'string' ? route.query.token : ''

const password = ref('')
const error = ref('')
const done = ref(false)
const busy = ref(false)

async function submit() {
  error.value = ''
  busy.value = true
  try {
    await authApi.resetPassword(email, token, password.value)
    done.value = true
  } catch (e) {
    error.value = e instanceof ApiError ? e.message : 'Something went wrong. Check your connection and try again.'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="auth-screen">
    <div class="auth-card card">
      <div class="auth-brand">
        <span class="app-brand-sub">RESELL TRACKER</span>
        <h1>New password</h1>
      </div>

      <template v-if="done">
        <p class="muted">Your password is changed.</p>
        <RouterLink class="btn btn-primary btn-block" to="/sign-in">Sign in</RouterLink>
      </template>

      <template v-else-if="!email || !token">
        <div class="auth-error" role="alert">This link is incomplete. Ask for a new one from the sign-in page.</div>
        <RouterLink class="btn btn-block" to="/sign-in">Back to sign in</RouterLink>
      </template>

      <form v-else class="auth-form" @submit.prevent="submit">
        <div v-if="error" class="auth-error" role="alert">{{ error }}</div>
        <p class="muted">For {{ email }}</p>
        <div class="field">
          <label for="new-password">New password</label>
          <input id="new-password" v-model="password" class="input" type="password" autocomplete="new-password" minlength="6" required />
        </div>
        <button class="btn btn-primary btn-block" type="submit" :disabled="busy">{{ busy ? 'Working…' : 'Set password' }}</button>
      </form>
    </div>
  </div>
</template>
