import { inject } from 'vue'
import { ToastKey } from './keys'

// `showToast(message, type)` — a no-op outside a ToastProvider.
export function useToast() {
  return inject(ToastKey, () => {})
}
