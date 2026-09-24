import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), vueDevTools()],
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
})
