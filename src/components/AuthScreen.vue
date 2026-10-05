<script setup>
import { ref } from 'vue'
import { useAuth } from '../composables/useAuth'

const MESSAGES = {
  'auth/invalid-credential': 'That email and password do not match an account.',
  'auth/invalid-email': 'That does not look like an email address.',
  'auth/email-already-in-use': 'There is already an account with that email.',
  'auth/weak-password': 'Use at least 6 characters.',
  'auth/too-many-requests': 'Too many attempts. Wait a minute and try again.',
  'auth/operation-not-allowed': 'Enable this sign-in method in the Firebase console first.',
}

const { login, signup, loginAnonymously, resetPassword } = useAuth()
const mode = ref('login')
const email = ref('')
const password = ref('')
const displayName = ref('')
const error = ref('')
const notice = ref('')
const busy = ref(false)

async function run(action) {
  error.value = ''
  notice.value = ''
  busy.value = true
  try {
    await action()
  } catch (err) {
    console.error(err)
    error.value = MESSAGES[err.code] || err.message || 'Something went wrong.'
  } finally {
    busy.value = false
  }
}

function handleSubmit() {
  run(() => (mode.value === 'login'
    ? login(email.value, password.value)
    : signup(email.value, password.value, displayName.value.trim() || email.value.split('@')[0])))
}

function handleReset() {
  if (!email.value) {
    error.value = 'Enter your email first, then tap reset.'
    return
  }
  run(async () => {
    await resetPassword(email.value)
    notice.value = 'Password reset email sent.'
  })
}
</script>

<template>
  <div class="auth-screen">
    <div class="auth-card card">
      <div class="auth-brand">
        <span class="app-brand-sub">INVENTORY · SALES · PROFIT</span>
        <h1>Resell Tracker</h1>
        <p class="muted" style="margin: 0; font-size: 13.5px">
          One place for what you bought, what is listed, and what you actually kept.
        </p>
      </div>

      <div v-if="error" class="auth-error">{{ error }}</div>
      <p v-if="notice" class="muted" style="margin: 0; font-size: 13px">{{ notice }}</p>

      <form class="auth-form" @submit.prevent="handleSubmit">
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
          <input id="password" v-model="password" class="input" type="password"
            :autocomplete="mode === 'login' ? 'current-password' : 'new-password'" required />
        </div>

        <button class="btn btn-primary btn-block" type="submit" :disabled="busy">
          {{ busy ? 'Working…' : mode === 'login' ? 'Sign in' : 'Create account' }}
        </button>
      </form>

      <div class="auth-switch">
        <template v-if="mode === 'login'">
          No account?<button @click="mode = 'signup'">Sign up</button>
          ·<button @click="handleReset">Forgot password</button>
        </template>
        <template v-else>
          Already have one?<button @click="mode = 'login'">Sign in</button>
        </template>
      </div>

      <div class="auth-divider">OR</div>

      <button class="btn btn-block" :disabled="busy" @click="run(loginAnonymously)">
        Try it as a guest
      </button>
    </div>
  </div>
</template>
