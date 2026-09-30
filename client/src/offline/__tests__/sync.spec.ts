import { describe, it, expect } from 'vitest'
import { ApiError } from '@/api'
import { itemDto } from '@/__tests__/fixtures'
import type { QueuedOp } from '../db'
import { sendOp, type SyncApi } from '../sync'

const op = (overrides: Partial<QueuedOp> = {}): QueuedOp => ({
  seq: 1, userId: 'u1', itemId: 'a', kind: 'update', payload: null, baseVersion: '"v1"',
  label: 'Tee', createdAt: '2026-09-30T10:00:00Z', state: 'pending', ...overrides,
})

/** A server that fails the send with this error, and answers GET with `current`. */
function server(error: unknown, current: ReturnType<typeof itemDto> | null = null): SyncApi {
  return {
    send: () => Promise.reject(error),
    get: () => Promise.resolve(current),
  }
}

describe('sendOp', () => {
  it('reports success with the server copy', async () => {
    const saved = itemDto({ id: 'a' })
    const api: SyncApi = { send: () => Promise.resolve(saved), get: () => Promise.resolve(null) }

    expect(await sendOp(op(), '"v1"', api)).toEqual({ kind: 'synced', item: saved })
  })

  it('treats a failed fetch, or a gateway with nothing behind it, as offline', async () => {
    expect((await sendOp(op(), '"v1"', server(new TypeError('Failed to fetch')))).kind).toBe('offline')
    expect((await sendOp(op(), '"v1"', server(new ApiError(504, 'Gateway Timeout', undefined)))).kind).toBe('offline')
  })

  it('turns a stale version into a conflict carrying the server copy', async () => {
    const theirs = itemDto({ id: 'a', title: 'Theirs', version: '"v2"' })

    expect(await sendOp(op(), '"v1"', server(new ApiError(412, 'Stale', 'changed'), theirs))).toEqual({ kind: 'conflict', theirs })
  })

  it('counts a replayed create the server already has as done', async () => {
    const existing = itemDto({ id: 'a' })

    expect(await sendOp(op({ kind: 'create' }), '', server(new ApiError(409, 'Duplicate item', 'exists'), existing))).toEqual({ kind: 'synced', item: existing })
  })

  it('counts deleting something already gone as done', async () => {
    expect(await sendOp(op({ kind: 'delete' }), '"v1"', server(new ApiError(404, 'Not found', undefined)))).toEqual({ kind: 'synced', item: null })
  })

  it('says so when the item was deleted on another device', async () => {
    const result = await sendOp(op(), '"v1"', server(new ApiError(412, 'Stale', 'changed'), null))

    expect(result).toEqual({ kind: 'failed', error: 'This item was deleted on another device.' })
  })

  it('reports a change the server refuses, with its reason', async () => {
    const result = await sendOp(op({ kind: 'sell' }), '"v1"', server(new ApiError(409, 'Not allowed', "A donated item can't be sold.")))

    expect(result).toEqual({ kind: 'failed', error: "A donated item can't be sold." })
  })

  it('stops when the session has ended', async () => {
    expect((await sendOp(op(), '"v1"', server(new ApiError(401, 'Unauthorized', undefined)))).kind).toBe('signedOut')
  })
})
