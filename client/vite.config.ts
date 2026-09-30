import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
import { VitePWA } from 'vite-plugin-pwa'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    vueDevTools(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.ico', 'favicon.svg', 'apple-touch-icon-180x180.png'],
      manifest: {
        name: 'Resell Tracker',
        short_name: 'Resell',
        description: 'Inventory, sales and profit tracking for Depop, eBay and Vinted',
        theme_color: '#d2a24c',
        background_color: '#0e0e10',
        display: 'standalone',
        orientation: 'portrait',
        scope: '/',
        start_url: '/',
        icons: [
          { src: 'pwa-64x64.png', sizes: '64x64', type: 'image/png' },
          { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
          { src: 'maskable-icon-512x512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        ],
      },
      workbox: {
        // The app shell is precached, so the app opens with no connection at all.
        globPatterns: ['**/*.{js,css,html,ico,png,svg,woff2}'],
        cleanupOutdatedCaches: true,
        clientsClaim: true,
        navigateFallback: '/index.html',
        // The API is deliberately left out of the service worker: no route matches
        // /api, so those requests never enter it and the browser makes them
        // natively. Offline reads come from the app's own IndexedDB snapshot, and
        // offline writes from its outbox (src/offline), which know about users and
        // versions in a way a response cache can't.
        navigateFallbackDenylist: [/^\/api\//],
        runtimeCaching: [
          {
            urlPattern: /^https:\/\/fonts\.(googleapis|gstatic)\.com\/.*/i,
            handler: 'CacheFirst',
            options: {
              cacheName: 'google-fonts',
              expiration: { maxEntries: 10, maxAgeSeconds: 60 * 60 * 24 * 365 },
            },
          },
        ],
      },
    }),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      // Files both halves of the app use: the platform registry and the profit fixture.
      '@shared': fileURLToPath(new URL('../shared', import.meta.url)),
    },
  },
  server: {
    port: 5176,
    fs: { allow: ['.', '../shared'] },
    // In development the API runs separately; proxying keeps requests (and
    // cookies) same-origin, matching production where ASP.NET Core serves the SPA.
    proxy: {
      '/api': 'http://localhost:5086',
    },
  },
  preview: {
    port: 4176,
    proxy: {
      '/api': 'http://localhost:5086',
    },
  },
})
