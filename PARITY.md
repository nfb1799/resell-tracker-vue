# Parity checklist

Everything the original [Resell Tracker](https://github.com/nfb1799/resell-tracker)
does, ticked off as the port matches it. Where the handoff and the original code
disagree, the original code wins; those spots are marked **(orig)**.

## Profit math

- [x] `gross = price + shippingCharged`
- [x] `net = payout - cost - shippingCost - otherCosts`
- [x] Payout entered: `fees = gross - payout`, exact; rate table ignored entirely
- [x] Payout null: fees from the platform rate, `payout = gross - fees`, marked estimated
- [x] A zero payout is a real payout, not a missing one
- [x] Payout above gross yields negative fees
- [x] Fee base is price + shipping charged when the schedule `includesShipping`, else price alone **(orig)**
- [x] Fee is 0 (not the bare fixed fee) when the base is 0
- [x] Unknown platform falls back to the `other` schedule **(orig)**
- [x] Margin = net / gross (null when gross is 0); ROI = net / cost (null when cost is 0)
- [x] Totals: gross, payout, fees, costs, net, count, estimated count
- [x] Write-off total = sum of donated cost, count; never mixed into sale profit
- [x] Projected net at asking price on the first listed platform, price only; null with no list price
- [x] Estimated fees round to the cent with halves going up, in exact decimal/integer-cents math. The original rounded a few halfway prices a cent low through floating point (eBay $150: $20.27 instead of $20.28); the fixture pins the corrected values
- [x] Every case above lives in `/shared/profit-cases.json`, passing in both C# and TS

## Platforms and fees

- [x] Registry: Depop 3.3% + $0.45, eBay 13.25% + $0.40, Vinted 0%, Other 0% **(orig: Other)**
- [x] Notes copied from `src/lib/platforms.js`
- [x] Per-user editable percent, fixed fee and includes-shipping flag, seeded from defaults; missing platforms filled from defaults **(orig: includes shipping)**
- [x] Adding a platform = one entry in `shared/platforms.json` (read by server and client) + `--<id>-color` token in both themes; filters, badges, charts, fee editor follow. Badges read the token directly, so unlike the original no per-platform `.badge-<id>` CSS rule is needed

## Item lifecycle

- [x] Statuses: inventory, listed, sold, donated
- [x] Adding the first platform: inventory → listed, listed date defaults to today **(orig)**
- [x] Removing every platform: listed → inventory **(orig)**
- [x] Sell from any on-hand item, inventory or listed **(orig)**
- [x] Edit an existing sale (sold → sold) **(orig)**
- [x] Donate from inventory or listed
- [x] Undo sale / undo donation → listed if it still has platforms, else inventory
- [x] "On hand" (cash tied up, aging) = inventory + listed only

## Inventory

- [x] Fields: photo, title, brand, size, category, condition, cost, source, acquired date, platforms, list price, listed date, notes
- [x] Conditions: New with tags, New without tags, Excellent, Good, Fair, For parts; default Excellent
- [ ] New item defaults from one shared field-definition module (also used by import): `shared/item-fields.json`, read by the form and the API; import in Phase 5
- [x] Search across title, brand, category, size, source, notes and donation org; filter by status (with counts) and platform
- [x] Platform filter matches a sold item by the platform it sold on, anything else by where it is listed **(orig)**
- [x] Sort: newest, longest listed, price high/low, title A–Z **(orig)**
- [x] Stale-listing flag (listed 45+ days)
- [x] Listed but unsold items show "est. net if it sells at asking"
- [x] Delete item (photo goes with it)

## Sales

- [x] Sold action opens the sale prefilled with asking price and first listed platform
- [x] `listedFor` snapshotted at sale time; `price` is the accepted offer
- [x] Live breakdown: markdown from asking, fees, cost of goods, shipping, net, margin, ROI, days held
- [x] Accepted offer required (> 0)
- [x] A payout above what the buyer paid is flagged as a likely typo
- [x] "est." label everywhere an estimated fee appears
- [x] Sales grouped by month with a running total

## Donations

- [x] Date, organization, optional receipt value (stored as typed, used in no calculation)
- [x] Leaves cash tied up and aging buckets immediately
- [x] Write-off shown on its own dashboard tile

## Dashboard and trends

- [x] Stat tiles (four across on desktop)
- [x] Monthly profit goal progress
- [x] Net profit by month
- [x] Profit by platform
- [x] Cash in unsold stock by age bucket
- [x] Profit by category

## Settings

- [x] Currency, theme (dark/light), monthly profit goal, display name **(orig)**
- [x] Fee editor for every registry platform

## Export and import

- [ ] CSV: every item, the original's 30 columns in order, including `Fees estimated`; UTF-8 BOM
- [ ] Raw JSON backup
- [ ] Bulk import from pasted JSON or `.json` file; single object accepted
- [ ] Only `title` (or `name`) required; defaults from the shared field module
- [ ] Accepts written names (`sourcedFrom`, `askingPrice`, `listingPlatform`) and the app's own
- [ ] Platform and condition matched case-insensitively by id or label
- [ ] Money accepts `20`, `"20"`, `"$20.50"`; rejects non-numeric with a reason
- [ ] Per-row failures (bad condition, unknown platform, non-numeric cost, malformed date, missing title) while valid rows import
- [ ] Photo from `https://` URL or `data:image/...;base64`; fetch failure is a warning, not a failure
- [ ] Summary ("11 added, 1 skipped")
- [ ] Validated on the server too; same service path as form-created items
- [ ] Round trip: export then import produces identical items

## Photos

- [x] Browser-side resize: ~96px thumbnail, ~900px full JPEG under 100KB, EXIF rotation applied, white matte for transparency
- [x] Rejects non-images and files over 25MB
- [x] Thumbnail in list queries; full photo fetched only when one item is opened
- [x] One photo per item; removable

## Layout and theme

- [x] Below 1024px: bottom tab bar, floating add button, stacked cards
- [x] 1024px and up: sidebar, page header, four stat tiles, dense sortable tables
- [x] All desktop CSS behind one media query; `useIsDesktop()` keeps one structure in the DOM
- [x] Dark and light themes with the original design tokens

## Auth, offline, demo

- [x] Email/password sign up (name defaults to the start of the email), sign in, sign out
- [ ] Password reset by emailed link **(orig)**; API and reset page done, the link is logged until an email provider is chosen with hosting (Phase 7)
- [x] 6-character minimum password **(orig)**, and the original's error wording
- [x] Every query scoped to the signed-in owner; cross-user isolation test
- [ ] Installable PWA
- [ ] Reads work offline from cache; writes queue and sync on reconnect
- [ ] Offline indicator
- [x] "Try the demo" from the sign-in page (replaces the original's guest sign-in): a private seeded copy per visitor, deleted after 24 hours
