// The items store working offline, end to end: changes made with no connection
// go to the outbox (a real IndexedDB, in memory), show immediately, and reach a
// server that enforces versions (FakeServer) once the connection is back.

import { beforeEach, describe, it, expect, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { reactive } from 'vue'
import { itemsApi } from '@/api'
import { cents } from '@/domain/money'
import type { ItemFields } from '@/domain/item'
import { FakeServer } from '@/offline/__tests__/fakeServer'
import { forgetUser, queuedOps } from '@/offline/db'
import { useAuthStore } from '../auth'
import { useConnectionStore } from '../connection'
import { useItemsStore } from '../items'

vi.mock('@/api', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api')>()
  return { ...actual, itemsApi: { ...actual.itemsApi, list: vi.fn<typeof actual.itemsApi.list>() } }
})

const USER = { id: 'u1', email: 'u1@example.test', displayName: 'U', isDemo: false, demoExpiresAt: null }

const fields = (overrides: Partial<ItemFields> = {}): ItemFields => ({
  title: 'Tee', brand: '', category: '', size: '', condition: 'Good', notes: '', cost: cents(800), source: '',
  acquiredDate: '2026-09-01', platforms: [], listPrice: null, listedDate: null, ...overrides,
})

let server: FakeServer

/** A store as the app has it after loading, talking to the fake server. */
async function start() {
  setActivePinia(createPinia())
  useAuthStore().me = USER
  const store = useItemsStore()
  store.useApi(server)
  vi.mocked(itemsApi.list).mockImplementation(async () => {
    if (server.offline) throw new TypeError('Failed to fetch')
    return [...server.items.values()]
  })
  await store.load()
  return store
}

function goOffline() {
  server.offline = true
  useConnectionStore().markOffline()
}

async function goOnline() {
  server.offline = false
  useConnectionStore().markOnline()
  await useItemsStore().flush()
}

beforeEach(async () => {
  server = new FakeServer()
  await forgetUser(USER.id)
})

describe('offline items', () => {
  it('keeps a change made offline, shows it at once, and sends it when back online', async () => {
    const store = await start()
    goOffline()

    const made = await store.create(fields({ title: 'Made offline', platforms: ['ebay'], listPrice: cents(4000) }))

    expect(made.unsynced).toBe(true)
    expect(made.status).toBe('listed')
    expect(await queuedOps(USER.id)).toHaveLength(1)
    expect(server.items.size).toBe(0)

    await goOnline()

    expect(server.items.get(made.id)?.title).toBe('Made offline')
    expect(store.byId.get(made.id)?.unsynced).toBe(false)
    expect(await queuedOps(USER.id)).toHaveLength(0)
  })

  it('queues what the item form hands over, reactive arrays and all', async () => {
    const store = await start()
    goOffline()
    // The form holds its fields in reactive(); IndexedDB can't store Vue's proxies.
    const form = reactive(fields({ title: 'From the form', platforms: ['depop'] }))

    const made = await store.create(form)

    expect(made.title).toBe('From the form')
    expect(await queuedOps(USER.id)).toHaveLength(1)
  })

  it('replays a chain of changes in order, each against the version the last one produced', async () => {
    const store = await start()
    goOffline()
    const made = await store.create(fields({ platforms: ['ebay'], listPrice: cents(4000) }))
    await store.sell(store.byId.get(made.id)!, {
      platform: 'ebay', listedFor: cents(4000), price: cents(3800), payout: null,
      shippingCharged: cents(0), shippingCost: cents(0), otherCosts: cents(0), date: '2026-09-30',
    })

    expect(store.byId.get(made.id)?.status).toBe('sold')

    await goOnline()

    expect(server.received.map((r) => r.kind)).toEqual(['create', 'sell'])
    // The sale was queued against an item that had no version yet; it goes out
    // against the one the create produced.
    expect(server.received[1]!.version).toBe('"v1"')
    expect(server.items.get(made.id)?.status).toBe('sold')
  })

  it('survives a restart: the outbox and the last items come back from the device', async () => {
    const first = await start()
    goOffline()
    await first.create(fields({ title: 'Still here after a reload' }))

    const second = await start() // a fresh page, still offline

    expect(second.items.map((i) => i.title)).toContain('Still here after a reload')
    expect(second.pendingCount).toBe(1)
  })

  it('holds a change that conflicts with an edit made elsewhere, and shows the server version meanwhile', async () => {
    const original = server.seed({ id: 'a', title: 'Tee', status: 'inventory' })
    const store = await start()
    goOffline()
    await store.update(store.byId.get('a')!, fields({ title: 'Tee, edited offline' }))
    server.offline = false
    server.editElsewhere('a', { title: 'Tee, edited on the laptop' })

    await goOnline()

    expect(store.issues).toHaveLength(1)
    expect(store.issues[0]!.state).toBe('conflict')
    expect(store.byId.get('a')?.title).toBe('Tee, edited on the laptop')
    expect(store.byId.get('a')?.syncIssue).toBe(true)
    expect(original.version).not.toBe(server.items.get('a')!.version)
  })

  it('keep mine applies my change on top of the latest version', async () => {
    server.seed({ id: 'a', title: 'Tee' })
    const store = await start()
    goOffline()
    await store.update(store.byId.get('a')!, fields({ title: 'Mine' }))
    server.offline = false
    server.editElsewhere('a', { title: 'Theirs' })
    await goOnline()

    await store.keepMine(store.issues[0]!)

    expect(server.items.get('a')?.title).toBe('Mine')
    expect(store.issues).toHaveLength(0)
    expect(await queuedOps(USER.id)).toHaveLength(0)
  })

  it("keep mine applies only my edit, leaving the other device's changes in place", async () => {
    server.seed({ id: 'a', title: 'Tee', condition: 'Good', cost: 8, listPrice: 40, platforms: [] })
    const store = await start()
    goOffline()
    await store.update(store.byId.get('a')!, fields({ title: 'Tee, renamed offline', listPrice: cents(4000) }))
    server.offline = false
    server.editElsewhere('a', { listPrice: 32 })
    await goOnline()

    await store.keepMine(store.issues[0]!)

    expect(server.items.get('a')).toMatchObject({ title: 'Tee, renamed offline', listPrice: 32 })
  })

  it('keep theirs drops my change, and later changes to the item go on top of theirs', async () => {
    server.seed({ id: 'a', title: 'Tee', platforms: ['depop'], status: 'listed' })
    const store = await start()
    goOffline()
    await store.update(store.byId.get('a')!, fields({ title: 'Mine', platforms: ['depop'] }))
    await store.donate(store.byId.get('a')!, { date: '2026-09-30', org: 'Goodwill', receiptValue: null })
    server.offline = false
    server.editElsewhere('a', { title: 'Theirs' })
    await goOnline()

    await store.discard(store.issues[0]!)

    expect(server.items.get('a')).toMatchObject({ title: 'Theirs', status: 'donated' })
    expect(store.issues).toHaveLength(0)
  })

  it('treats a change the server already has as done, not as a conflict', async () => {
    // Seeded with exactly what the form sends, so the title is the only change.
    server.seed({ id: 'a', title: 'Tee', condition: 'Good', cost: 8, acquiredDate: '2026-09-01', platforms: [] })
    const store = await start()
    goOffline()
    await store.update(store.byId.get('a')!, fields({ title: 'Same everywhere' }))
    server.offline = false
    // The first attempt reached the server but its response was lost.
    server.editElsewhere('a', { title: 'Same everywhere' })

    await goOnline()

    expect(store.issues).toHaveLength(0)
    expect(await queuedOps(USER.id)).toHaveLength(0)
  })

  it('reports a change the server refuses and lets it be discarded', async () => {
    server.seed({ id: 'a', status: 'listed', platforms: ['depop'] })
    const store = await start()
    goOffline()
    await store.sell(store.byId.get('a')!, {
      platform: 'depop', listedFor: cents(0), price: cents(1000), payout: null,
      shippingCharged: cents(0), shippingCost: cents(0), otherCosts: cents(0), date: '2026-09-30',
    })
    server.offline = false
    // Donated elsewhere; the fake server then refuses the sale (the version moved too, so it
    // first shows as a conflict: keeping mine sends it again and the refusal comes back).
    server.editElsewhere('a', { status: 'donated' })
    await goOnline()
    await store.keepMine(store.issues[0]!)

    expect(store.issues[0]).toMatchObject({ state: 'failed', error: "A donated item can't be sold. Undo the donation first." })

    await store.discard(store.issues[0]!)

    expect(store.issues).toHaveLength(0)
    expect(server.items.get('a')?.status).toBe('donated')
  })

  it('goes back to waiting if the connection drops halfway, then carries on', async () => {
    const store = await start()
    goOffline()
    const made = await store.create(fields())
    await store.update(store.byId.get(made.id)!, fields({ title: 'Renamed' }))
    // The create lands, then the connection goes before the rename.
    const send = server.send.bind(server)
    server.send = async (op, version) => {
      const result = await send(op, version)
      server.offline = true
      return result
    }
    server.offline = false
    useConnectionStore().markOnline() // starts a sync by itself
    await store.flush()

    expect(server.items.get(made.id)?.title).toBe('Tee')
    expect(store.pendingCount).toBe(1)

    server.send = send
    await goOnline()

    expect(server.items.get(made.id)?.title).toBe('Renamed')
  })
})
