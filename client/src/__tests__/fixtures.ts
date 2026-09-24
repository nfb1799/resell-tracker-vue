// Test builders: an API item and the client item it becomes.

import type { ItemDto } from '@/api/types'
import { fromDto, type Item } from '@/domain/item'

export function itemDto(overrides: Partial<ItemDto> = {}): ItemDto {
  return {
    id: crypto.randomUUID(),
    status: 'inventory',
    title: 'Tee',
    brand: '',
    category: '',
    size: '',
    condition: 'Excellent',
    notes: '',
    cost: 0,
    source: '',
    acquiredDate: '2026-09-01',
    platforms: [],
    listPrice: null,
    listedDate: null,
    thumbnail: null,
    sale: null,
    donation: null,
    profit: null,
    projectedNet: null,
    daysListed: null,
    createdAt: '2026-09-01T12:00:00Z',
    updatedAt: '2026-09-01T12:00:00Z',
    version: '"AAAAAAAAB9E="',
    ...overrides,
  }
}

export const item = (overrides: Partial<ItemDto> = {}): Item => fromDto(itemDto(overrides))
