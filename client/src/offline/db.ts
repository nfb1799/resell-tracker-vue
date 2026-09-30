// The device's own copy of a user's data, in IndexedDB: the last thing the server
// said (so the app opens and reads with no connection) and the outbox of changes
// made since (so nothing typed offline is lost). Everything is keyed by user, and
// signing out removes that user's rows.

import { openDB, type DBSchema, type IDBPDatabase } from 'idb'
import type { DonationRequestDto, ItemDto, ItemRequestDto, MeDto, SaleRequestDto } from '@/api/types'

export type OpKind = 'create' | 'update' | 'sell' | 'undoSale' | 'donate' | 'undoDonation' | 'delete' | 'setPhoto' | 'removePhoto'

export interface PhotoPayload {
  thumbnail: Blob
  full: Blob
  /** The thumbnail as a data: URL, so the list can show it before it syncs. */
  thumbnailUrl: string
}

export type OpPayload = ItemRequestDto | SaleRequestDto | DonationRequestDto | PhotoPayload | null

export type OpState = 'pending' | 'conflict' | 'failed'

/** One change waiting to reach the server, in the order it was made. */
export interface QueuedOp {
  seq?: number
  userId: string
  itemId: string
  kind: OpKind
  payload: OpPayload
  /** The version the change was made against; null for an item created offline. */
  baseVersion: string | null
  /** The item's title when the change was made, for messages. */
  label: string
  createdAt: string
  state: OpState
  /** Why a failed change was refused. */
  error?: string
  /** The server's copy when this change was found to conflict with it. */
  theirs?: ItemDto
  /**
   * For an edit: the item as it stood when the change was made, so a conflict can
   * tell the fields this change touched from the ones it merely carried along.
   */
  base?: ItemDto
}

export interface Snapshot {
  items: ItemDto[]
  savedAt: string
}

interface OfflineDb extends DBSchema {
  /** Keyed "<userId>:<name>" (items, settings, fees, dashboard, trends), plus "me" for the last user. */
  snapshot: { key: string; value: unknown }
  outbox: { key: number; value: QueuedOp; indexes: { byUser: string } }
}

let db: Promise<IDBPDatabase<OfflineDb>> | null = null

function open() {
  db ??= openDB<OfflineDb>('resell-tracker', 1, {
    upgrade(database) {
      database.createObjectStore('snapshot')
      database.createObjectStore('outbox', { keyPath: 'seq', autoIncrement: true }).createIndex('byUser', 'userId')
    },
  })
  return db
}

export async function readSnapshot<T>(userId: string, name: string): Promise<T | undefined> {
  return (await (await open()).get('snapshot', `${userId}:${name}`)) as T | undefined
}

export async function writeSnapshot(userId: string, name: string, value: unknown): Promise<void> {
  await (await open()).put('snapshot', value, `${userId}:${name}`)
}

/** Who was last signed in on this device, so the app can open offline. */
export async function readLastUser(): Promise<MeDto | undefined> {
  return (await (await open()).get('snapshot', 'me')) as MeDto | undefined
}

export async function writeLastUser(me: MeDto): Promise<void> {
  await (await open()).put('snapshot', me, 'me')
}

export async function queuedOps(userId: string): Promise<QueuedOp[]> {
  const ops = await (await open()).getAllFromIndex('outbox', 'byUser', userId)
  return ops.sort((a, b) => a.seq! - b.seq!)
}

export async function enqueue(op: QueuedOp): Promise<QueuedOp> {
  const seq = await (await open()).add('outbox', op)
  return { ...op, seq }
}

export async function saveOp(op: QueuedOp): Promise<void> {
  await (await open()).put('outbox', op)
}

export async function removeOp(seq: number): Promise<void> {
  await (await open()).delete('outbox', seq)
}

/** Forgets everything this device holds for a user. */
export async function forgetUser(userId: string): Promise<void> {
  const database = await open()
  const tx = database.transaction(['snapshot', 'outbox'], 'readwrite')
  const snapshot = tx.objectStore('snapshot')
  for (const key of await snapshot.getAllKeys()) {
    if (key === 'me' || key.startsWith(`${userId}:`)) await snapshot.delete(key)
  }
  const outbox = tx.objectStore('outbox')
  for (const seq of await outbox.index('byUser').getAllKeys(userId)) await outbox.delete(seq)
  await tx.done
}
