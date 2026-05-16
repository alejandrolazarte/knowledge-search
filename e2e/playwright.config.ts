import { defineConfig } from '@playwright/test'
import path from 'path'

const port = process.env.E2E_PORT ?? '5120'
const baseURL = process.env.E2E_BASE_URL ?? `http://localhost:${port}`

export default defineConfig({
  testDir:        './tests',
  globalSetup:    './global-setup.ts',
  fullyParallel:  false,
  workers:        1,
  retries:        process.env.CI ? 2 : 0,
  reporter: [
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
    ['list'],
  ],
  use: {
    baseURL,
    video:      'on',
    screenshot: 'only-on-failure',
    trace:      'on-first-retry',
  },
  webServer: {
    command:             'dotnet run',
    cwd:                 path.join(__dirname, '..', 'src', 'Api'),
    url:                 baseURL,
    timeout:             90_000,
    reuseExistingServer: !process.env.CI,
    env: {
      ASPNETCORE_ENVIRONMENT: 'E2E',
      ASPNETCORE_URLS:        baseURL,
      KNOWLEDGE_DB:           path.join(__dirname, 'test.db'),
      SOURCES_CONFIG:         path.join(__dirname, 'sources.json'),
    },
  },
})
