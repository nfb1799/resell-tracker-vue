import { describe, it, expect } from 'vitest'
import { cents, divideRoundHalfUp, formatPercent, formatSigned, parseMoney, type Cents } from '../money'
import { computeProfit, markdown } from '../profit'
import { defaultFeeSettings } from '../platforms'

describe('parseMoney', () => {
  it.each([
    ['12', 1200],
    ['12.5', 1250],
    ['12.50', 1250],
    ['0.29', 29], // 0.29 * 100 is 28.999… in floating point
    ['$1,204.50', 120450],
    ['£8', 800],
    [' 3.10 ', 310],
    ['.5', 50],
    ['-4.25', -425],
    ['1.005', 101], // past the cent rounds half up
    ['1.004', 100],
    [20, 2000],
  ] as const)('reads %j as %i cents', (input, expected) => {
    expect(parseMoney(input)).toBe(expected)
  })

  it.each(['', '   ', 'twenty', '12.5.1', '-', '.', '1e3'])('treats %j as not entered', (input) => {
    expect(parseMoney(input)).toBeNull()
  })

  it('treats null and undefined as not entered', () => {
    expect(parseMoney(null)).toBeNull()
    expect(parseMoney(undefined)).toBeNull()
  })
})

describe('divideRoundHalfUp', () => {
  it('rounds halves up, toward positive infinity, like the server', () => {
    expect(divideRoundHalfUp(15, 10)).toBe(2)
    expect(divideRoundHalfUp(14, 10)).toBe(1)
    expect(divideRoundHalfUp(-15, 10)).toBe(-1)
    expect(divideRoundHalfUp(-16, 10)).toBe(-2)
  })
})

describe('cents', () => {
  it('refuses a fractional amount', () => {
    expect(() => cents(12.5)).toThrow(RangeError)
  })
})

describe('sale form values', () => {
  it('reads string form values, blanks as zero, without producing NaN', () => {
    const value = (s: string) => parseMoney(s) ?? (0 as Cents)
    const p = computeProfit(
      value('8'),
      {
        platform: 'ebay',
        price: value('40'),
        shippingCharged: value('5'),
        payout: parseMoney('38.64'),
        shippingCost: value(''),
        otherCosts: value(''),
      },
      defaultFeeSettings(),
    )
    expect(p.fees).toBe(636)
    expect(p.net).toBe(3064)
  })

  it('leaves a blank payout as "not known yet", so fees are estimated', () => {
    expect(parseMoney('')).toBeNull()
  })
})

describe('markdown', () => {
  it('reports how far under asking the offer landed', () => {
    const m = markdown(cents(4000), cents(3200))
    expect(m?.amount).toBe(800)
    expect(m?.fraction).toBeCloseTo(0.2, 9)
  })

  it('is null at or above asking, or with either side missing', () => {
    expect(markdown(cents(4000), cents(4000))).toBeNull()
    expect(markdown(cents(4000), cents(4500))).toBeNull()
    expect(markdown(cents(0), cents(3000))).toBeNull()
    expect(markdown(cents(4000), cents(0))).toBeNull()
  })
})

describe('formatting', () => {
  it('signs deltas', () => {
    expect(formatSigned(cents(1420))).toMatch(/^\+.*14\.20$/)
    expect(formatSigned(cents(-300))).toMatch(/^-.*3\.00$/)
  })

  it('shows a dash for a ratio that does not exist', () => {
    expect(formatPercent(null)).toBe('—')
    expect(formatPercent(0.254, 1)).toBe('25.4%')
  })
})
