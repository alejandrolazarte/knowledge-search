import { defineConfig } from '@playwright/test'
import path from 'path'

export default defineConfig({
  testDir:        './tests',
  globalSetup:    './global-setup.ts',
  fullyParallel:  false,
  retries:        process.env.CI ? 2 : 0,
  reporter: [
    ['html', { outputFolder: 'playwright-report', open: 'never' }],
    ['list'],
  ],
  use: {
    baseURL:    'http://localhost:5111',
    video:      'on',
    screenshot: 'only-on-failure',
    trace:      'on-first-retry',
  },
  webServer: {
    command:             'dotnet run',
    cwd:                 path.join(__dirname, '..', 'src', 'Api'),
    url:                 'http://localhost:5111',
    timeout:             90_000,
    reuseExistingServer: !process.env.CI,
    env:                 { ASPNETCORE_ENVIRONMENT: 'E2E' },
  },
})
