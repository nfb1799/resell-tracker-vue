import { onScopeDispose, ref, type Ref } from 'vue'

/**
 * Chart.js draws on a canvas and needs real colour values, not var(--x), so this
 * resolves the design tokens off the root element and resolves them again
 * whenever the theme attribute flips.
 */
export function useThemeColors<const T extends readonly string[]>(names: T): Ref<Record<T[number], string>> {
  const read = () => {
    const styles = getComputedStyle(document.documentElement)
    return Object.fromEntries(names.map((n) => [n, styles.getPropertyValue(`--${n}`).trim()])) as Record<T[number], string>
  }

  const colors = ref(read()) as Ref<Record<T[number], string>>
  const observer = new MutationObserver(() => {
    colors.value = read()
  })
  observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] })
  onScopeDispose(() => observer.disconnect())
  return colors
}
