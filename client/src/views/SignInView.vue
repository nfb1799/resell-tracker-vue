<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ApiError, authApi } from '@/api'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const mode = ref<'login' | 'signup'>('login')
const email = ref('')
const password = ref('')
const displayName = ref('')
const error = ref('')
const notice = ref('')
const busy = ref(false)

async function run(action: () => Promise<void>, after?: () => Promise<unknown>) {
  error.value = ''
  notice.value = ''
  busy.value = true
  try {
    await action()
    await after?.()
  } catch (e) {
    error.value = e instanceof ApiError ? e.message : 'Something went wrong. Check your connection and try again.'
  } finally {
    busy.value = false
  }
}

const enterApp = () => router.replace(typeof route.query.next === 'string' ? route.query.next : '/')

function submit() {
  void run(
    () =>
      mode.value === 'login'
        ? auth.login(email.value, password.value)
        : auth.register(email.value, password.value, displayName.value.trim() || email.value.split('@')[0] || ''),
    enterApp,
  )
}

function forgot() {
  if (!email.value) {
    error.value = 'Enter your email first, then tap reset.'
    return
  }
  void run(async () => {
    await authApi.forgotPassword(email.value)
    notice.value = 'If that email has an account, a reset link is on its way.'
  })
}
</script>

<template>
  <div class="auth-screen">
    <div class="auth-card card">
      <div class="auth-brand">
        <span class="app-brand-sub">INVENTORY · SALES · PROFIT</span>
        <h1>Resell Tracker</h1>
        <p class="muted tagline">One place for what you bought, what is listed, and what you actually kept.</p>
      </div>

      <div v-if="error" class="auth-error" role="alert">{{ error }}</div>
      <p v-if="notice" class="muted notice" role="status">{{ notice }}</p>

      <form class="auth-form" @submit.prevent="submit">
        <div v-if="mode === 'signup'" class="field">
          <label for="name">Name</label>
          <input id="name" v-model="displayName" class="input" autocomplete="name" />
        </div>
        <div class="field">
          <label for="email">Email</label>
          <input id="email" v-model="email" class="input" type="email" autocomplete="email" required />
        </div>
        <div class="field">
          <label for="password">Password</label>
          <input
            id="password"
            v-model="password"
            class="input"
            type="password"
            :autocomplete="mode === 'login' ? 'current-password' : 'new-password'"
            required
          />
        </div>
        <button class="btn btn-primary btn-block" type="submit" :disabled="busy">
          {{ busy ? 'Working…' : mode === 'login' ? 'Sign in' : 'Create account' }}
        </button>
      </form>

      <div class="auth-switch">
        <template v-if="mode === 'login'">
          No account?<button @click="mode = 'signup'">Sign up</button>·<button @click="forgot">Forgot password</button>
        </template>
        <template v-else>Already have one?<button @click="mode = 'login'">Sign in</button></template>
      </div>

      <div class="auth-divider">OR</div>

      <button class="btn btn-block" :disabled="busy" @click="run(auth.startDemo, enterApp)">Try the demo</button>
      <p class="dimmed demo-note">A private copy of a sample inventory, yours for a day. No account needed.</p>
    </div>
  </div>
</template>

<style scoped>
.tagline { margin: 0; font-size: 13.5px; }
.notice { margin: 0; font-size: 13px; }
.demo-note { margin: -4px 0 0; font-size: 12px; text-align: center; }
</style>
