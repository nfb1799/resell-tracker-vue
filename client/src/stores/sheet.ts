import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Item } from '@/domain/item'

export type SheetMode = 'new' | 'edit' | 'sell' | 'donate'

/** One sheet at a time, over whichever page is showing. */
export const useSheetStore = defineStore('sheet', () => {
  const mode = ref<SheetMode | null>(null)
  const itemId = ref<string | null>(null)

  function open(next: SheetMode, item?: Item) {
    mode.value = next
    itemId.value = item?.id ?? null
  }

  /**
   * Tapping a finished item goes straight to how it ended; anything still on the
   * shelf opens the editor, which can hand off to either outcome.
   */
  function openItem(item: Item) {
    open(item.status === 'sold' ? 'sell' : item.status === 'donated' ? 'donate' : 'edit', item)
  }

  function close() {
    mode.value = null
    itemId.value = null
  }

  return { mode, itemId, open, openItem, close }
})
