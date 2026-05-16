import { test, expect, type APIRequestContext } from '@playwright/test'
import path from 'path'

const KNOWLEDGE_PATH = path.join(__dirname, '..', 'fixtures', 'knowledge')

async function configureKnowledgeSource(request: APIRequestContext) {
  await request.put('/sources', {
    data: {
      version: 1,
      sources: [
        {
          id: 'knowledge',
          name: 'knowledge',
          kind: 'Knowledge',
          hostPath: KNOWLEDGE_PATH,
          indexCode: false,
          indexDocs: true,
        },
      ],
    },
  })
  await request.post('/index')
}

test.describe('Knowledge Search', () => {
  test.beforeEach(async ({ page, request }) => {
    await configureKnowledgeSource(request)
    await page.goto('/')
  })

  test('muestra el input de búsqueda al cargar', async ({ page }) => {
    await expect(page.getByPlaceholder(/Buscar en knowledge/i)).toBeVisible()
  })

  test('devuelve resultados para una búsqueda válida', async ({ page }) => {
    await page.getByPlaceholder(/Buscar en knowledge/i).fill('integration events')
    await page.keyboard.press('Enter')

    await expect(page.getByText(/resultado/i)).toBeVisible({ timeout: 10_000 })
    await expect(page.locator('.bg-gh-surface.border.border-gh-border.rounded-lg').first()).toBeVisible()
  })

  test('muestra el título y la ruta del resultado', async ({ page }) => {
    await page.getByPlaceholder(/Buscar en knowledge/i).fill('UserCreatedIntegrationEvent')
    await page.keyboard.press('Enter')

    await expect(page.getByText(/UserCreatedIntegrationEvent/i).first()).toBeVisible({ timeout: 10_000 })
  })

  test('devuelve resultados para búsqueda de autenticación', async ({ page }) => {
    await page.getByPlaceholder(/Buscar en knowledge/i).fill('autenticación')
    await page.keyboard.press('Enter')

    await expect(page.getByText(/resultado/i)).toBeVisible({ timeout: 10_000 })
    await expect(page.getByText(/Autenticación/i).first()).toBeVisible()
  })

  test('muestra estado vacío cuando no hay resultados', async ({ page }) => {
    await page.getByPlaceholder(/Buscar en knowledge/i).fill('xyzterminoquenoexiste999')
    await page.keyboard.press('Enter')

    await expect(page.getByText(/Sin resultados/i).first()).toBeVisible({ timeout: 10_000 })
  })

  test('limpia la búsqueda con Escape', async ({ page }) => {
    const input = page.getByPlaceholder(/Buscar en knowledge/i)
    await input.fill('algo')
    await page.keyboard.press('Escape')

    await expect(input).toHaveValue('')
  })
})
