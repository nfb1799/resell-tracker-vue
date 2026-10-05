import { ref, onMounted, onUnmounted } from 'vue'

// Chart.js paints to a canvas and needs real colour values, not `var(--x)`, so
// resolve the tokens off the root element and re-resolve whenever the theme
// attribute flips.
export function useThemeColors(names) {
  const read = () => {
    const styles = getComputedStyle(document.documentElement)
    return Object.fromEntries(names.map(n => [n, styles.getPropertyValue(`--${n}`).trim()]))
  }

  const colors = ref(read())
  let observer = null

  onMounted(() => {
    observer = new MutationObserver(() => { colors.value = read() })
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] })
  })
  onUnmounted(() => observer?.disconnect())

  return colors
}
