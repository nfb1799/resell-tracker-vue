import { inject } from 'vue'
import { ItemsKey } from './keys'

// The live item list and settings as refs, plus the CRUD helpers.
export function useItems() {
  return inject(ItemsKey)
}
