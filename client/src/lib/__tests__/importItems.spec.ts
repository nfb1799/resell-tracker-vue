import { describe, it, expect } from 'vitest'
import fixture from '@shared/import-cases.json'
import { MAX_ROWS, parseImport, readRow, withoutPhotos } from '../importItems'

// The same cases the server's ImportCaseTests run, so the preview and the
// server's verdict agree.
type Expected = { label: string; errors?: string[]; item?: unknown; photo?: unknown }
const cases = fixture.cases.map((c) => ({ name: c.name, row: c.row as unknown, expected: c.expected as Expected }))

describe('readRow (shared cases): rows that import', () => {
  it.each(cases.filter((c) => !c.expected.errors).map((c) => [c.name, c] as const))('%s', (_, c) => {
    const row = readRow(c.row, 0, fixture.today)

    expect(row.rowNumber).toBe(1)
    expect(row.label).toBe(c.expected.label)
    expect(row.errors).toEqual([])
    expect(row.item).toEqual(c.expected.item)
    expect(row.photo).toEqual(c.expected.photo)
  })
})

describe('readRow (shared cases): rows that are skipped', () => {
  it.each(cases.filter((c) => c.expected.errors).map((c) => [c.name, c] as const))('%s', (_, c) => {
    const row = readRow(c.row, 0, fixture.today)

    expect(row.label).toBe(c.expected.label)
    expect(row.errors).toEqual(c.expected.errors)
    expect(row.item).toBeNull()
  })
})

describe('parseImport', () => {
  const sample = { title: 'Nautica Polo', listingPlatform: 'Depop', askingPrice: 20 }

  it('splits an array into valid and invalid rows, keeping both', () => {
    const result = parseImport(JSON.stringify([sample, { brand: 'no title' }, { ...sample, title: 'Two' }]))
    if (result.fatal !== null) throw new Error(result.fatal)
    expect(result.valid).toHaveLength(2)
    expect(result.invalid).toHaveLength(1)
    expect(result.invalid[0]!.rowNumber).toBe(2)
  })

  it('accepts a single object as a one-item import', () => {
    const result = parseImport(JSON.stringify(sample))
    expect(result.fatal === null && result.valid.length).toBe(1)
  })

  it('reads the items out of a backup file', () => {
    const result = parseImport(JSON.stringify({ exportedAt: '2026-09-30', settings: {}, items: [sample, sample] }))
    expect(result.fatal === null && result.rows.length).toBe(2)
  })

  it('reports invalid JSON without throwing', () => {
    expect(parseImport('[{title: nope}]').fatal).toMatch(/not valid JSON/)
  })

  it('reports an empty array, empty input and too many rows', () => {
    expect(parseImport('[]').fatal).toMatch(/empty/)
    expect(parseImport('   ').fatal).toMatch(/Nothing to import/)
    expect(parseImport(JSON.stringify(Array.from({ length: MAX_ROWS + 1 }, () => sample))).fatal).toMatch(/at most/)
  })

  it('numbers rows from one, so the report matches what a person counts', () => {
    const result = parseImport(JSON.stringify([{ title: 'a' }, { title: 'b' }]))
    expect(result.fatal === null && result.rows.map((r) => r.rowNumber)).toEqual([1, 2])
  })
})

describe('withoutPhotos', () => {
  it('drops every photo field and keeps the rest, leaving non-objects alone', () => {
    expect(withoutPhotos([{ title: 'a', photo: 'data:image/png;base64,AAA', imageUrl: 'https://x' }, 'text'])).toEqual([{ title: 'a' }, 'text'])
  })
})
