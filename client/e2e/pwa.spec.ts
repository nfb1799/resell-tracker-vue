import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { chromium, expect, test } from '@playwright/test'

// Stands in for Lighthouse's old PWA audit (removed in Lighthouse 12): the
// browser itself says whether the app meets its install criteria.
//
// Playwright's usual pages run in incognito-like contexts, where Chrome never
// offers to install anything, so this one uses an ordinary, throwaway profile.
test('the browser reports no installability errors', async ({ baseURL }, testInfo) => {
  const profile = mkdtempSync(join(tmpdir(), 'pwa-profile-'))
  const context = await chromium.launchPersistentContext(profile, { channel: testInfo.project.use.channel })
  try {
    const page = await context.newPage()
    await page.goto(baseURL!)
    await page.evaluate(() => navigator.serviceWorker.ready)

    const cdp = await context.newCDPSession(page)
    const { installabilityErrors } = await cdp.send('Page.getInstallabilityErrors')

    expect(installabilityErrors).toEqual([])
  } finally {
    await context.close()
    rmSync(profile, { recursive: true, force: true })
  }
})

test('the manifest names the app and carries install-sized icons', async ({ page, request }) => {
  await page.goto('/')
  const href = await page.locator('link[rel="manifest"]').getAttribute('href')
  const manifest = await (await request.get(href!)).json()

  expect(manifest).toMatchObject({ name: 'Resell Tracker', display: 'standalone', start_url: '/' })
  const sizes = manifest.icons.map((i: { sizes: string }) => i.sizes)
  expect(sizes).toEqual(expect.arrayContaining(['192x192', '512x512']))
})

test('the app opens with no connection once it has been visited', async ({ page, context }) => {
  await page.goto('/')
  await page.evaluate(() => navigator.serviceWorker.ready)
  // Let the service worker take control of the page before cutting the network.
  await page.reload()

  await context.setOffline(true)
  await page.reload()

  // Nobody has signed in on this browser, so the app shell lands on sign-in.
  await expect(page.getByRole('heading', { name: 'Resell Tracker' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Try the demo' })).toBeVisible()
})
