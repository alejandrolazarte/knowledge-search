import fs from 'fs'
import path from 'path'

export default async function globalSetup() {
  const testDbPath = path.join(__dirname, 'test.db')
  const walPath    = testDbPath + '-wal'
  const shmPath    = testDbPath + '-shm'

  for (const filePath of [testDbPath, walPath, shmPath]) {
    if (fs.existsSync(filePath)) {
      fs.unlinkSync(filePath)
    }
  }
}
