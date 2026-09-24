import { onScopeDispose, readonly, ref, type Ref } from 'vue'

/**
 * Layout that differs structurally (a sidebar instead of a tab bar, a table
 * instead of stacked cards) has to be decided in script, not just hidden with
 * CSS, so only one version is ever in the DOM.
 */
export function useMediaQuery(query: string): Readonly<Ref<boolean>> {
  const list = window.matchMedia(query)
  const matches = ref(list.matches)
  const onChange = (event: MediaQueryListEvent) => {
    matches.value = event.matches
  }
  list.addEventListener('change', onChange)
  onScopeDispose(() => list.removeEventListener('change', onChange))
  return readonly(matches)
}

/** Where a sidebar and a data table start beating a tab bar and cards. Matches desktop.css. */
export const DESKTOP_QUERY = '(min-width: 1024px)'

export const useIsDesktop = () => useMediaQuery(DESKTOP_QUERY)
