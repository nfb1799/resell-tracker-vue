// The client's item model: the API's item with every amount in cents. Money is
// converted here, once, on the way in and out, so nothing above this layer ever
// holds a dollar float.

import type { ItemDto, ItemRequestDto, ProfitDto, SaleDto } from '@/api/types'
import { fromDollars, toDollars, type Cents } from './money'
import type { ItemStatus } from './lifecycle'
import type { ProfitBreakdown, SaleFigures } from './profit'
import itemFields from '@shared/item-fields.json'

export const CONDITIONS: readonly string[] = itemFields.conditions
export const DEFAULT_CONDITION: string = itemFields.defaultCondition
export const MAX_LENGTH = itemFields.maxLength

export interface Sale extends SaleFigures {
  listedFor: Cents
  date: string
}

export interface Donation {
  date: string
  org: string
  receiptValue: Cents | null
}

export interface Item {
  id: string
  status: ItemStatus
  title: string
  brand: string
  category: string
  size: string
  condition: string
  notes: string
  cost: Cents
  source: string
  acquiredDate: string | null
  platforms: string[]
  listPrice: Cents | null
  listedDate: string | null
  /** The ~96px thumbnail as a data: URL. */
  thumbnail: string | null
  sale: Sale | null
  donation: Donation | null
  /** The server's breakdown, for sold items. */
  profit: ProfitBreakdown | null
  projectedNet: Cents | null
  createdAt: string
  version: string
}

const nullableCents = (n: number | null): Cents | null => (n === null ? null : fromDollars(n))

const toSale = (s: SaleDto): Sale => ({
  platform: s.platform,
  listedFor: fromDollars(s.listedFor),
  price: fromDollars(s.price),
  payout: nullableCents(s.payout),
  shippingCharged: fromDollars(s.shippingCharged),
  shippingCost: fromDollars(s.shippingCost),
  otherCosts: fromDollars(s.otherCosts),
  date: s.date,
})

const toProfit = (p: ProfitDto): ProfitBreakdown => ({
  gross: fromDollars(p.gross),
  payout: fromDollars(p.payout),
  fees: fromDollars(p.fees),
  cogs: fromDollars(p.cogs),
  shippingCost: fromDollars(p.shippingCost),
  otherCosts: fromDollars(p.otherCosts),
  costs: fromDollars(p.costs),
  net: fromDollars(p.net),
  margin: p.margin,
  roi: p.roi,
  feesEstimated: p.feesEstimated,
})

export function fromDto(dto: ItemDto): Item {
  return {
    id: dto.id,
    status: dto.status,
    title: dto.title,
    brand: dto.brand,
    category: dto.category,
    size: dto.size,
    condition: dto.condition,
    notes: dto.notes,
    cost: fromDollars(dto.cost),
    source: dto.source,
    acquiredDate: dto.acquiredDate,
    platforms: dto.platforms,
    listPrice: nullableCents(dto.listPrice),
    listedDate: dto.listedDate,
    thumbnail: dto.thumbnail,
    sale: dto.sale && toSale(dto.sale),
    donation: dto.donation && { ...dto.donation, receiptValue: nullableCents(dto.donation.receiptValue) },
    profit: dto.profit && toProfit(dto.profit),
    projectedNet: nullableCents(dto.projectedNet),
    createdAt: dto.createdAt,
    version: dto.version,
  }
}

/** An item's own fields, as the New item and edit forms hold them. */
export interface ItemFields {
  title: string
  brand: string
  category: string
  size: string
  condition: string
  notes: string
  cost: Cents
  source: string
  acquiredDate: string | null
  platforms: string[]
  listPrice: Cents | null
  listedDate: string | null
}

export function toRequest(fields: ItemFields, id?: string): ItemRequestDto {
  return {
    ...(id ? { id } : {}),
    ...fields,
    cost: toDollars(fields.cost),
    listPrice: fields.listPrice === null ? null : toDollars(fields.listPrice),
  }
}
