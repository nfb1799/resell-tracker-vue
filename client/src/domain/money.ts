// Money on the client is always a whole number of cents. Arithmetic on cents is
// exact; dollars only appear at the edges: parsing what someone typed, reading
// and writing the API, and formatting for display.
//
// `Cents` is branded so a dollar amount can't be passed where cents are expected
// without going through one of the converters below.

export type Cents = number & { readonly __brand: 'Cents' }

export const ZERO = 0 as Cents

export function cents(n: number): Cents {
  if (!Number.isSafeInteger(n)) throw new RangeError(`Not a whole number of cents: ${n}`)
  return n as Cents
}

// Sums and differences of cents stay whole, so these need no rounding.
export const add = (...values: Cents[]): Cents => values.reduce((a, b) => a + b, 0) as Cents
export const sub = (a: Cents, b: Cents): Cents => (a - b) as Cents

/** A dollar amount from the API or a fixture (at most 2 decimals) to cents. */
export const fromDollars = (dollars: number): Cents => cents(Math.round(dollars * 100))

/** Cents to the dollar number the API expects. */
export const toDollars = (value: Cents): number => value / 100

/**
 * Divides a whole number by a positive one, rounding halves up (toward positive
 * infinity) like the server's Money.RoundToCent, without going through a float.
 */
export function divideRoundHalfUp(numerator: number, denominator: number): number {
  const remainder = ((numerator % denominator) + denominator) % denominator
  const quotient = (numerator - remainder) / denominator
  return remainder * 2 >= denominator ? quotient + 1 : quotient
}

const MONEY_PATTERN = /^(-)?(\d*)(?:\.(\d*))?$/

/**
 * What someone typed into a money field, as cents. Accepts "12", "12.5", "$1,204.50"
 * and "£8"; anything past the cent rounds half up. Blank or unusable input is null,
 * so a caller decides whether that means zero or "not entered" (as with a payout).
 */
export function parseMoney(input: string | number | null | undefined): Cents | null {
  if (input === null || input === undefined) return null
  if (typeof input === 'number') return Number.isFinite(input) ? fromDollars(input) : null

  const cleaned = input.replace(/[$£€,\s]/g, '')
  const match = MONEY_PATTERN.exec(cleaned)
  if (!match) return null
  const [, minus, whole = '', fraction = ''] = match
  if (whole === '' && fraction === '') return null

  // Read the digits as a string so "0.29" is 29 cents, not 28.999…
  const padded = fraction.padEnd(3, '0')
  const tenthsOfCents = Number(whole || '0') * 1000 + Number(padded.slice(0, 3))
  const value = divideRoundHalfUp(tenthsOfCents, 10)
  return cents(minus ? -value : value)
}

export function formatMoney(value: Cents, currency = 'USD'): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency,
      maximumFractionDigits: 2,
    }).format(toDollars(value))
  } catch {
    return toDollars(value).toFixed(2)
  }
}

/** Compact signed form for deltas, e.g. "+$14.20" / "-$3.00". */
export function formatSigned(value: Cents, currency = 'USD'): string {
  const sign = value > 0 ? '+' : value < 0 ? '-' : ''
  return `${sign}${formatMoney(Math.abs(value) as Cents, currency)}`
}

export function formatPercent(fraction: number | null, digits = 0): string {
  if (fraction === null || !Number.isFinite(fraction)) return '—'
  return `${(fraction * 100).toFixed(digits)}%`
}
