// The API's shapes, exactly as they travel: money in dollars, dates as
// 'YYYY-MM-DD'. Nothing outside src/api uses these directly; src/domain/item.ts
// converts them to the client's cents-based model.

import type { ItemStatus } from '@/domain/lifecycle'
import type { FeeSchedule } from '@/domain/platforms'

export interface MeDto {
  id: string
  email: string
  displayName: string
  isDemo: boolean
  demoExpiresAt: string | null
}

export interface SaleDto {
  platform: string
  listedFor: number
  price: number
  payout: number | null
  shippingCharged: number
  shippingCost: number
  otherCosts: number
  date: string
}

export interface DonationDto {
  date: string
  org: string
  receiptValue: number | null
}

export interface ProfitDto {
  gross: number
  payout: number
  fees: number
  cogs: number
  shippingCost: number
  otherCosts: number
  costs: number
  net: number
  margin: number | null
  roi: number | null
  feesEstimated: boolean
}

export interface ItemDto {
  id: string
  status: ItemStatus
  title: string
  brand: string
  category: string
  size: string
  condition: string
  notes: string
  cost: number
  source: string
  acquiredDate: string | null
  platforms: string[]
  listPrice: number | null
  listedDate: string | null
  thumbnail: string | null
  sale: SaleDto | null
  donation: DonationDto | null
  profit: ProfitDto | null
  projectedNet: number | null
  daysListed: number | null
  createdAt: string
  updatedAt: string
  version: string
}

export interface ItemRequestDto {
  id?: string
  title: string
  brand: string
  category: string
  size: string
  condition: string
  notes: string
  cost: number
  source: string
  acquiredDate: string | null
  platforms: string[]
  listPrice: number | null
  listedDate: string | null
}

export interface SaleRequestDto {
  platform: string
  listedFor: number
  price: number
  payout: number | null
  shippingCharged: number
  shippingCost: number
  otherCosts: number
  date: string
}

export interface DonationRequestDto {
  date: string
  org: string
  receiptValue: number | null
}

export interface SettingsDto {
  displayName: string
  currency: string
  theme: 'dark' | 'light'
  profitGoal: number
}

export type FeeSettingsDto = Record<string, FeeSchedule>

export interface TotalsDto {
  gross: number
  payout: number
  fees: number
  costs: number
  net: number
  count: number
  estimated: number
}

export interface DashboardDto {
  month: TotalsDto
  allTime: TotalsDto
  writeOffs: { cost: number; count: number }
  tiedUp: number
  onHandCount: number
  listedCount: number
  listedValue: number
  avgDaysToSell: number | null
  monthMargin: number | null
}

export interface TrendsDto {
  months: { month: string; totals: TotalsDto }[]
  byPlatform: { platform: string; totals: TotalsDto }[]
  aging: { label: string; minDays: number; maxDays: number | null; count: number; cost: number }[]
  byCategory: { category: string; totals: TotalsDto; margin: number | null; avgDaysToSell: number | null }[]
  best: { title: string; net: number; cogs: number; price: number; platform: string; roi: number | null } | null
}
