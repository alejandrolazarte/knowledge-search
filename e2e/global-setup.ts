import fs from 'fs'
import path from 'path'

export default async function globalSetup() {
  const testDbPath = path.join(__dirname, 'test.db')
  const walPath    = testDbPath + '-wal'
  const shmPath    = testDbPath + '-shm'
  const sourcesPath = path.join(__dirname, 'sources.json')

  for (const filePath of [testDbPath, walPath, shmPath, sourcesPath]) {
    try {
      if (fs.existsSync(filePath)) {
        fs.unlinkSync(filePath)
      }
    } catch {
      // File may be locked by a previous run — ignore and continue
      console.warn(`Could not delete ${filePath}, continuing anyway`)
    }
  }
}
