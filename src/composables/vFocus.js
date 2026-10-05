// `v-focus` — focuses the element when it mounts, the way React's autoFocus
// does. The HTML `autofocus` attribute only fires on page load, not for an
// element a sheet inserts later. `v-focus="false"` opts out.
export const vFocus = {
  mounted(el, binding) {
    if (binding.value !== false) el.focus()
  },
}
