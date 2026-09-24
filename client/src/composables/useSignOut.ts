import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useItemsStore } from '@/stores/items'
import { useSettingsStore } from '@/stores/settings'
import { useSheetStore } from '@/stores/sheet'
import { useStatsStore } from '@/stores/stats'
import { useToastStore } from '@/stores/toast'

/** Signs out and drops everything held for that account, so the next one starts clean. */
export function useSignOut() {
  const router = useRouter()
  const auth = useAuthStore()
  const items = useItemsStore()
  const settings = useSettingsStore()
  const stats = useStatsStore()
  const sheet = useSheetStore()
  const toast = useToastStore()

  return async () => {
    try {
      await auth.logout()
    } catch {
      toast.show('Could not sign out. Check your connection.', 'error')
      return
    }
    sheet.close()
    items.clear()
    stats.clear()
    settings.clear()
    await router.push({ name: 'sign-in' })
  }
}
