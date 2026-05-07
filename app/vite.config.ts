import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../../src/Api',
    emptyOutDir: false,
    assetsDir: 'assets',
  },
  server: {
    proxy: {
      '/search': 'http://localhost:5111',
      '/skills': 'http://localhost:5111',
      '/index':  'http://localhost:5111',
    }
  }
})
