import { test, expect, type Page } from '@playwright/test'
import path from 'path'
import { waitForJobs } from './helpers/wait-jobs'

const USERS_MS_PATH        = path.join(__dirname, '..', 'fixtures', 'repos', 'users-ms')
const NOTIFICATIONS_MS_PATH = path.join(__dirname, '..', 'fixtures', 'repos', 'notifications-ms')

async function configureRepositorySources(page: Page) {
  const response = await page.request.put('/sources', {
    data: {
      version: 1,
      sources: [
        {
          id: 'users-ms',
          name: 'users-ms',
          kind: 'Repository',
          hostPath: USERS_MS_PATH,
          indexCode: true,
          indexDocs: false,
        },
        {
          id: 'notifications-ms',
          name: 'notifications-ms',
          kind: 'Repository',
          hostPath: NOTIFICATIONS_MS_PATH,
          indexCode: true,
          indexDocs: false,
        },
      ],
    },
  })
  expect(response.ok()).toBeTruthy()
  const body = await response.json() as { jobIds: string[] }
  await waitForJobs(page.request, body.jobIds)
}

async function navigateToGraphView(page: Page) {
  await configureRepositorySources(page)
  await page.goto('/')
  await page.getByRole('button', { name: /Code Graph/i }).click()
  await expect(page.getByPlaceholder(/Buscar en repos/i)).toBeVisible()
}

test.describe('Code Graph — vista lista', () => {
  test.beforeEach(async ({ page }) => {
    await navigateToGraphView(page)
  })

  test('muestra el panel de búsqueda y los controles de profundidad', async ({ page }) => {
    await expect(page.getByPlaceholder(/Buscar en repos/i)).toBeVisible()
    await expect(page.getByRole('button', { name: 'Directo' })).toBeVisible()
    await expect(page.getByRole('button', { name: '+2' })).toBeVisible()
  })

  test('escanea users-ms y muestra el repo en los chips', async ({ page }) => {
    await expect(page.getByRole('button', { name: 'users-ms' })).toBeVisible()
  })

  test('escanea ambos repos y busca nodos de IntegrationEvent', async ({ page }) => {
    await page.getByPlaceholder(/Buscar en repos/i).fill('IntegrationEvent')
    await page.keyboard.press('Enter')

    await expect(page.getByText(/UserCreatedIntegrationEvent/i).first()).toBeVisible({ timeout: 10_000 })
    await expect(page.getByText(/UserCreatedIntegrationEventHandler/i).first()).toBeVisible()
  })

  test('agrupa los nodos por repositorio', async ({ page }) => {
    await page.getByPlaceholder(/Buscar en repos/i).fill('IntegrationEvent')
    await page.keyboard.press('Enter')

    await expect(page.getByText('users-ms').first()).toBeVisible({ timeout: 10_000 })
    await expect(page.getByText('notifications-ms').first()).toBeVisible()
  })

  test('muestra conexiones cross-repo después de build cross-refs', async ({ page }) => {
    await page.getByRole('button', { name: /Build cross-refs/i }).click()
    await expect(page.getByText(/conexiones cross-repo encontradas/i)).toBeVisible({ timeout: 10_000 })

    await page.getByPlaceholder(/Buscar en repos/i).fill('IntegrationEvent')
    await page.keyboard.press('Enter')

    await expect(page.getByText(/^Conexiones cross-repo \(\d+\)$/i)).toBeVisible({ timeout: 10_000 })
  })

  test('retorna sin resultados para término inexistente', async ({ page }) => {
    await page.getByPlaceholder(/Buscar en repos/i).fill('XyzClaseQueNoExiste999')
    await page.keyboard.press('Enter')

    await expect(page.getByText(/Sin resultados/i).first()).toBeVisible({ timeout: 10_000 })
  })
})

test.describe('Code Graph — vista grafo SVG', () => {
  test('renderiza nodos SVG al cambiar a vista Grafo', async ({ page }) => {
    await navigateToGraphView(page)

    await page.getByPlaceholder(/Buscar en repos/i).fill('IntegrationEvent')
    await page.keyboard.press('Enter')
    await expect(page.getByText(/UserCreatedIntegrationEvent/i).first()).toBeVisible({ timeout: 10_000 })

    await page.getByRole('button', { name: /^Grafo$/i }).click()

    await expect(page.locator('svg .graph-node').first()).toBeVisible({ timeout: 5_000 })
    const nodeCount = await page.locator('svg .graph-node').count()
    expect(nodeCount).toBeGreaterThan(0)
  })
})
