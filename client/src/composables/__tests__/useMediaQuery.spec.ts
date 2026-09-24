import { afterEach, describe, it, expect, vi } from 'vitest'
import { effectScope } from 'vue'
import { useMediaQuery } from '../useMediaQuery'

function fakeMatchMedia(initial: boolean) {
  let listener: ((e: MediaQueryListEvent) => void) | null = null
  const list = {
    matches: initial,
    addEventListener: vi.fn<(type: string, fn: (e: MediaQueryListEvent) => void) => void>((_, fn) => {
      listener = fn
    }),
    removeEventListener: vi.fn<() => void>(() => {
      listener = null
    }),
  }
  vi.stubGlobal('matchMedia', vi.fn<() => typeof list>(() => list))
  return {
    list,
    fire: (matches: boolean) => listener?.({ matches } as MediaQueryListEvent),
    listening: () => listener !== null,
  }
}

afterEach(() => vi.unstubAllGlobals())

describe('useMediaQuery', () => {
  it('starts from the current match and follows changes', () => {
    const media = fakeMatchMedia(false)
    const scope = effectScope()
    const matches = scope.run(() => useMediaQuery('(min-width: 1024px)'))!

    expect(matches.value).toBe(false)
    media.fire(true)
    expect(matches.value).toBe(true)
    scope.stop()
  })

  it('stops listening when its component goes away', () => {
    const media = fakeMatchMedia(true)
    const scope = effectScope()
    scope.run(() => useMediaQuery('(min-width: 1024px)'))

    scope.stop()

    expect(media.listening()).toBe(false)
  })
})
