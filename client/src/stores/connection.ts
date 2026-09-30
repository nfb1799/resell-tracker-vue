import { defineStore } from 'pinia'
import { ref } from 'vue'

const RETRY_MS = 15_000

/**
 * Whether the server is reachable. The browser's online flag is only a hint (it
 * says nothing about the server itself), so a request failing for lack of a
 * network also marks the app offline, and one succeeding marks it back online.
 * While offline it also checks the server every so often, since the browser's
 * "online" event never fires when the network was fine but the server wasn't
 * (a restart, a captive portal).
 */
export const useConnectionStore = defineStore('connection', () => {
  const online = ref(navigator.onLine)
  const listeners: (() => void)[] = []
  let retry: ReturnType<typeof setInterval> | undefined

  async function probe() {
    try {
      const response = await fetch('/api/health', { cache: 'no-store' })
      if (response.ok) markOnline()
    } catch {
      // Still unreachable; the next tick tries again.
    }
  }

  function markOffline() {
    online.value = false
    retry ??= setInterval(() => void probe(), RETRY_MS)
  }

  function markOnline() {
    clearInterval(retry)
    retry = undefined
    if (online.value) return
    online.value = true
    listeners.forEach((fn) => fn())
  }

  /** Runs whenever the connection comes back. */
  function onReconnect(fn: () => void) {
    listeners.push(fn)
  }

  window.addEventListener('online', () => void probe())
  window.addEventListener('offline', markOffline)

  return { online, markOffline, markOnline, onReconnect }
})
