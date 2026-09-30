import { fileURLToPath } from 'node:url'
import { mergeConfig, defineConfig, configDefaults } from 'vitest/config'
import viteConfig from './vite.config.ts'

export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: 'happy-dom',
      exclude: [...configDefaults.exclude, 'e2e/**'],
      // An in-memory IndexedDB, so the offline snapshot and outbox run for real in tests.
      setupFiles: ['fake-indexeddb/auto'],
      root: fileURLToPath(new URL('./', import.meta.url)),
    },
  }),
)
