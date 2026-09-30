// Bulk import, on the client: the preview a user sees while pasting. The server
// runs the same rules (ResellTracker.Domain/Import.cs) before writing anything,
// and both pass shared/import-cases.json, so the preview and the verdict agree,
// error wording included.
//
// Rows use the names a person would write (sourcedFrom, askingPrice, a single
// listingPlatform) or this app's own (source, listPrice, platforms), so a backup
// export imports cleanly, sale and donation included. Money here is in dollars,
// as it is in the JSON; the preview converts for display.

import { getLocalDateString } from '@/domain/dates'
import { applyPlatforms, type ItemStatus } from '@/domain/lifecycle'
import { PLATFORMS } from '@/domain/platforms'
import { CONDITIONS, DEFAULT_CONDITION, MAX_LENGTH } from '@/domain/item'

export const MAX_ROWS = 1000
const MAX_AMOUNT = 99_999_999.99

export interface ImportedSale {
  platform: string
  listedFor: number
  price: number
  payout: number | null
  shippingCharged: number
  shippingCost: number
  otherCosts: number
  date: string
}

export interface ImportedDonation {
  date: string
  org: string
  receiptValue: number | null
}

export interface ImportedItem {
  title: string
  brand: string
  category: string
  size: string
  condition: string
  notes: string
  cost: number
  source: string
  acquiredDate: string
  platforms: string[]
  listPrice: number | null
  listedDate: string | null
  status: ItemStatus
  sale: ImportedSale | null
  donation: ImportedDonation | null
}

export interface ImportedPhoto {
  src: string
  kind: 'url' | 'dataUri'
}

export interface ImportRow {
  rowNumber: number
  label: string
  errors: string[]
  item: ImportedItem | null
  photo: ImportedPhoto | null
}

type Json = unknown
type JsonObject = Record<string, Json>

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/
const DATA_URI = /^data:image\/[a-z0-9.+-]+;base64,/i
const URL_PATTERN = /^https?:\/\//i
const NUMBER_TEXT = /^[+-]?(\d+\.?\d*|\.\d+)$/

const isObject = (v: Json): v is JsonObject => v !== null && typeof v === 'object' && !Array.isArray(v)
const isBlank = (v: Json) => v === undefined || v === null || (typeof v === 'string' && v.trim() === '')

function first(row: JsonObject, ...names: string[]): Json {
  for (const name of names) {
    if (!isBlank(row[name])) return row[name]
  }
  return undefined
}

function text(v: Json): string {
  if (isBlank(v)) return ''
  if (typeof v === 'string') return v.trim()
  return (typeof v === 'object' ? JSON.stringify(v) : String(v)).trim()
}

const quote = (v: Json) => JSON.stringify(v)

/** Places after the point, ignoring trailing zeros ("12.340" has 2). */
function decimals(numberText: string): number {
  const fraction = numberText.split('.')[1] ?? ''
  return fraction.replace(/0+$/, '').length
}

/**
 * Accepts 20, "20" and "$20.50"; refuses "twenty" rather than storing 0, since a
 * cost that silently becomes zero corrupts every profit figure downstream.
 */
function readMoney(v: Json, field: string, errors: string[]): number | null {
  if (isBlank(v)) return null

  let amount: number
  let digits: string
  if (typeof v === 'number') {
    if (!Number.isFinite(v)) {
      errors.push(`${field} is not a number`)
      return null
    }
    amount = v
    digits = String(Math.abs(v))
  } else if (typeof v === 'string') {
    const cleaned = v.replace(/[$£€,\s]/g, '')
    if (!NUMBER_TEXT.test(cleaned)) {
      errors.push(`${field} is not a number (got ${quote(v)})`)
      return null
    }
    amount = Number(cleaned)
    digits = cleaned.replace(/^[+-]/, '')
  } else {
    errors.push(`${field} is not a number`)
    return null
  }

  if (amount < 0) errors.push(`${field} can't be negative`)
  else if (digits.includes('e') || decimals(digits) > 2) errors.push(`${field} can't have more than 2 decimal places`)
  else if (amount > MAX_AMOUNT) errors.push(`${field} is too large`)
  else return amount
  return null
}

function readDate(v: Json, field: string, errors: string[]): string | null {
  if (isBlank(v)) return null
  const s = typeof v === 'string' ? v.trim() : null
  if (s === null || !DATE_PATTERN.test(s)) {
    errors.push(`${field} must look like 2026-08-28 (got ${quote(v)})`)
    return null
  }
  const [y, m, d] = s.split('-').map(Number) as [number, number, number]
  const date = new Date(Date.UTC(y, m - 1, d))
  if (date.getUTCFullYear() !== y || date.getUTCMonth() !== m - 1 || date.getUTCDate() !== d) {
    errors.push(`${field} is not a real date (got ${quote(v)})`)
    return null
  }
  return s
}

function readCondition(v: Json, errors: string[]): string | null {
  if (isBlank(v)) return null
  const wanted = text(v).toLowerCase()
  const match = CONDITIONS.find((c) => c.toLowerCase() === wanted)
  if (!match) errors.push(`condition ${quote(v)} is not one of ${CONDITIONS.join(', ')}`)
  return match ?? null
}

const platformList = () => PLATFORMS.map((p) => p.label).join(', ')

/** "Depop", "depop", "eBay" and "ebay" all land on the right id. */
function matchPlatform(value: string): string | null {
  const wanted = value.toLowerCase()
  return PLATFORMS.find((p) => p.id.toLowerCase() === wanted || p.label.toLowerCase() === wanted)?.id ?? null
}

function readPlatforms(v: Json, errors: string[]): string[] {
  const ids: string[] = []
  if (isBlank(v)) return ids
  for (const entry of Array.isArray(v) ? v : [v]) {
    if (isBlank(entry)) continue
    const id = matchPlatform(text(entry))
    if (!id) errors.push(`listingPlatform ${quote(entry)} is not one of ${platformList()}`)
    else if (!ids.includes(id)) ids.push(id)
  }
  return ids
}

/** A photo is somewhere to fetch from or an inline data URI; anything else is a typo worth reporting. */
function readPhoto(v: Json, errors: string[]): ImportedPhoto | null {
  if (isBlank(v)) return null
  const src = text(v)
  if (DATA_URI.test(src)) return { src, kind: 'dataUri' }
  if (URL_PATTERN.test(src)) return { src, kind: 'url' }
  errors.push('photo must be an http(s) URL or a data:image/…;base64 URI')
  return null
}

function readSale(v: Json, listPrice: number | null, errors: string[]): ImportedSale | null {
  if (isBlank(v)) return null
  if (!isObject(v)) {
    errors.push('sale must be an object')
    return null
  }
  const before = errors.length
  const platformValue = first(v, 'platform')
  const platform = isBlank(platformValue) ? null : matchPlatform(text(platformValue))
  if (!platform) {
    errors.push(isBlank(platformValue) ? 'sale.platform is required' : `sale.platform ${quote(platformValue)} is not one of ${platformList()}`)
  }
  const priceValue = first(v, 'price')
  const price = readMoney(priceValue, 'sale.price', errors)
  if (isBlank(priceValue)) errors.push('sale.price is required')
  else if (price === 0) errors.push('sale.price must be more than 0')
  const listedFor = readMoney(first(v, 'listedFor'), 'sale.listedFor', errors)
  const payout = readMoney(first(v, 'payout'), 'sale.payout', errors)
  const shippingCharged = readMoney(first(v, 'shippingCharged'), 'sale.shippingCharged', errors)
  const shippingCost = readMoney(first(v, 'shippingCost'), 'sale.shippingCost', errors)
  const otherCosts = readMoney(first(v, 'otherCosts'), 'sale.otherCosts', errors)
  const date = readDate(first(v, 'date'), 'sale.date', errors)
  if (isBlank(first(v, 'date'))) errors.push('sale.date is required')

  if (errors.length > before) return null
  return {
    platform: platform!,
    listedFor: listedFor ?? listPrice ?? 0,
    price: price!,
    payout,
    shippingCharged: shippingCharged ?? 0,
    shippingCost: shippingCost ?? 0,
    otherCosts: otherCosts ?? 0,
    date: date!,
  }
}

function readDonation(v: Json, errors: string[]): ImportedDonation | null {
  if (isBlank(v)) return null
  if (!isObject(v)) {
    errors.push('donation must be an object')
    return null
  }
  const before = errors.length
  const date = readDate(first(v, 'date'), 'donation.date', errors)
  if (isBlank(first(v, 'date'))) errors.push('donation.date is required')
  const org = text(first(v, 'org'))
  if (org.length > MAX_LENGTH.donationOrg) errors.push(`donation.org can't be longer than ${MAX_LENGTH.donationOrg} characters`)
  const receiptValue = readMoney(first(v, 'receiptValue'), 'donation.receiptValue', errors)
  return errors.length > before ? null : { date: date!, org, receiptValue }
}

/** One row in, one verdict out. Row numbers count from 1, as a person would. */
export function readRow(raw: Json, index: number, today = getLocalDateString()): ImportRow {
  const rowNumber = index + 1
  if (!isObject(raw)) return { rowNumber, label: `Row ${rowNumber}`, errors: ['not a JSON object'], item: null, photo: null }

  const errors: string[] = []
  const title = text(first(raw, 'title', 'name'))
  if (!title) errors.push('title is required')

  const condition = readCondition(first(raw, 'condition'), errors)
  const platforms = readPlatforms(first(raw, 'listingPlatform', 'listingPlatforms', 'platform', 'platforms'), errors)
  const cost = readMoney(first(raw, 'cost'), 'cost', errors)
  const listPrice = readMoney(first(raw, 'askingPrice', 'listPrice'), 'askingPrice', errors)
  const acquiredDate = readDate(first(raw, 'acquiredDate'), 'acquiredDate', errors)
  const listedDate = readDate(first(raw, 'listedDate'), 'listedDate', errors)
  const photo = readPhoto(first(raw, 'photo', 'photoUrl', 'image', 'imageUrl'), errors)
  const sale = readSale(first(raw, 'sale'), listPrice, errors)
  const donation = readDonation(first(raw, 'donation'), errors)
  if (sale && donation) errors.push("a row can't have both a sale and a donation")

  const fields = {
    title: [title, MAX_LENGTH.title],
    brand: [text(first(raw, 'brand')), MAX_LENGTH.brand],
    category: [text(first(raw, 'category')), MAX_LENGTH.category],
    size: [text(first(raw, 'size')), MAX_LENGTH.size],
    sourcedFrom: [text(first(raw, 'sourcedFrom', 'source')), MAX_LENGTH.source],
    notes: [text(first(raw, 'notes')), MAX_LENGTH.notes],
  } as const
  for (const [name, [value, max]] of Object.entries(fields)) {
    if (value.length > max) errors.push(`${name} can't be longer than ${max} characters`)
  }

  const label = title || `Row ${rowNumber}`
  if (errors.length) return { rowNumber, label, errors, item: null, photo: null }

  // Naming a platform is what makes an item listed in the form, and the listed
  // date defaults to today there too; a sale or donation ends it.
  const listing = applyPlatforms('inventory', platforms, listedDate, today)
  return {
    rowNumber,
    label,
    errors: [],
    photo,
    item: {
      title,
      brand: fields.brand[0],
      category: fields.category[0],
      size: fields.size[0],
      condition: condition ?? DEFAULT_CONDITION,
      notes: fields.notes[0],
      cost: cost ?? 0,
      source: fields.sourcedFrom[0],
      acquiredDate: acquiredDate ?? today,
      platforms,
      listPrice,
      listedDate: listing.listedDate,
      status: sale ? 'sold' : donation ? 'donated' : listing.status,
      sale,
      donation,
    },
  }
}

/** The rows of an import: an array, a single object, or a backup's { items: [...] }. */
export function importRows(root: Json): Json[] | null {
  if (Array.isArray(root)) return root
  if (isObject(root)) return Array.isArray(root.items) ? root.items : [root]
  return null
}

export type ParsedImport =
  | { fatal: string }
  | { fatal: null; raw: Json[]; rows: ImportRow[]; valid: ImportRow[]; invalid: ImportRow[] }

/** Text (pasted or read off a file) to rows ready to import, or why nothing can be. */
export function parseImport(input: string, today = getLocalDateString()): ParsedImport {
  const trimmed = input.trim()
  if (!trimmed) return { fatal: 'Nothing to import yet — paste a JSON array or choose a file.' }

  let parsed: Json
  try {
    parsed = JSON.parse(trimmed)
  } catch (error) {
    return { fatal: `That is not valid JSON: ${error instanceof Error ? error.message : String(error)}` }
  }

  const raw = importRows(parsed)
  if (!raw) return { fatal: 'Paste a JSON array of items.' }
  if (raw.length === 0) return { fatal: 'The array is empty — nothing to import.' }
  if (raw.length > MAX_ROWS) return { fatal: `That is ${raw.length} rows; import at most ${MAX_ROWS} at a time.` }

  const rows = raw.map((r, i) => readRow(r, i, today))
  return { fatal: null, raw, rows, valid: rows.filter((r) => !r.errors.length), invalid: rows.filter((r) => r.errors.length) }
}

/**
 * What gets sent to the server: every row (so its row numbers match), with the
 * photo left out, since photos are fetched and uploaded from here afterwards.
 */
export function withoutPhotos(raw: Json[]): Json[] {
  return raw.map((row) => {
    if (!isObject(row)) return row
    const copy: JsonObject = { ...row }
    for (const key of ['photo', 'photoUrl', 'image', 'imageUrl']) delete copy[key]
    return copy
  })
}
