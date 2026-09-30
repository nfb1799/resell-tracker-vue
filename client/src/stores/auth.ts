import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { ApiError, authApi, type MeDto } from '@/api'
import { forgetUser, readLastUser, writeLastUser } from '@/offline/db'
import { isNetworkError } from '@/offline/sync'

/**
 * Who is signed in. The session itself is an HttpOnly cookie the browser holds;
 * this mirrors what /api/auth/me says about it. With no connection, the last user
 * signed in on this device carries on, so the app opens offline; the server has
 * the final word as soon as it can be reached.
 */
export const useAuthStore = defineStore('auth', () => {
  const me = ref<MeDto | null>(null)
  let checked: Promise<void> | null = null

  const signedIn = computed(() => me.value !== null)

  function remember(user: MeDto) {
    me.value = user
    void writeLastUser(user).catch(() => {})
  }

  /** Asks the server once whether the cookie is still good. Safe to call repeatedly. */
  function ensureChecked(): Promise<void> {
    checked ??= authApi.me().then(remember, async (error: unknown) => {
      if (error instanceof ApiError && error.status === 401) {
        me.value = null
      } else if (isNetworkError(error)) {
        me.value = (await readLastUser().catch(() => undefined)) ?? null
      } else {
        throw error
      }
    })
    return checked
  }

  async function login(email: string, password: string) {
    remember(await authApi.login(email, password))
  }

  async function register(email: string, password: string, displayName: string) {
    remember(await authApi.register(email, password, displayName))
  }

  async function startDemo() {
    remember(await authApi.demo())
  }

  /** Signs out and removes everything this device held for the account. */
  async function logout() {
    const id = me.value?.id
    await authApi.logout()
    me.value = null
    if (id) await forgetUser(id).catch(() => {})
  }

  /** The session ended on the server (expired, or a demo was deleted). */
  function forget() {
    me.value = null
  }

  return { me, signedIn, ensureChecked, login, register, startDemo, logout, forget }
})
