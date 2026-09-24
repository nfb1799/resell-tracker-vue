import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { ApiError, authApi, type MeDto } from '@/api'

/**
 * Who is signed in. The session itself is an HttpOnly cookie the browser holds;
 * this only mirrors what /api/auth/me says about it.
 */
export const useAuthStore = defineStore('auth', () => {
  const me = ref<MeDto | null>(null)
  let checked: Promise<void> | null = null

  const signedIn = computed(() => me.value !== null)

  /** Asks the server once whether the cookie is still good. Safe to call repeatedly. */
  function ensureChecked(): Promise<void> {
    checked ??= authApi.me().then(
      (user) => {
        me.value = user
      },
      (error: unknown) => {
        if (!(error instanceof ApiError && error.status === 401)) throw error
        me.value = null
      },
    )
    return checked
  }

  async function login(email: string, password: string) {
    me.value = await authApi.login(email, password)
  }

  async function register(email: string, password: string, displayName: string) {
    me.value = await authApi.register(email, password, displayName)
  }

  async function startDemo() {
    me.value = await authApi.demo()
  }

  async function logout() {
    await authApi.logout()
    me.value = null
  }

  /** The session ended on the server (expired, or a demo was deleted). */
  function forget() {
    me.value = null
  }

  return { me, signedIn, ensureChecked, login, register, startDemo, logout, forget }
})
