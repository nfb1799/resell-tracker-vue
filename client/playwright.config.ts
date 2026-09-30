import { defineConfig } from '@playwright/test'

// Browser checks that only a real Chromium can answer: is the app installable,
// and does it open with no connection. They use the Chromium already on the
// machine (Chrome on CI runners, Edge on Windows), so nothing is downloaded.
//
// BASE_URL points them at a deployed site; without it they build the app and
// serve it locally.
const baseURL = process.env.BASE_URL ?? 'http://localhost:4177'

export default defineConfig({
  testDir: 'e2e',
  timeout: 60_000,
  use: {
    baseURL,
    channel: process.env.PLAYWRIGHT_CHANNEL ?? (process.platform === 'win32' ? 'msedge' : 'chrome'),
  },
  webServer: process.env.BASE_URL
    ? undefined
    : {
        command: 'npm run build-only && npx vite preview --port 4177 --strictPort',
        url: baseURL,
        reuseExistingServer: !process.env.CI,
        timeout: 120_000,
      },
})
