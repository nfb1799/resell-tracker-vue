import { ref, onMounted, onUnmounted } from 'vue'

// Layout that differs structurally — a sidebar instead of a tab bar, a table
// instead of stacked cards — has to be decided in JS, not just hidden with CSS,
// so only one version is ever in the DOM.
//
// Read synchronously on setup rather than in onMounted, so the first paint is
// already at the right size.
export function useMediaQuery(query) {
  const list = window.matchMedia(query)
  const matches = ref(list.matches)
  const onChange = () => { matches.value = list.matches }
  onMounted(() => list.addEventListener('change', onChange))
  onUnmounted(() => list.removeEventListener('change', onChange))
  return matches
}

// The point where a sidebar and a data table start beating a tab bar and cards.
export const DESKTOP_QUERY = '(min-width: 1024px)'

export const useIsDesktop = () => useMediaQuery(DESKTOP_QUERY)
