import { inject } from 'vue'
import { AuthKey } from './keys'

// { currentUser, userProfile } as refs, plus the sign-in/out actions.
export function useAuth() {
  return inject(AuthKey)
}
