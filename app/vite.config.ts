import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../src/Api',
    emptyOutDir: false,
    assetsDir: 'assets',
  },
  server: {
    proxy: {
      '/search': 'http://localhost:5111',
      '/skills': 'http://localhost:5111',
      '/index':  'http://localhost:5111',
      '/file':   'http://localhost:5111',
      '/image':  'http://localhost:5111',
      '/log':    'http://localhost:5111',
      '/roots':  'http://localhost:5111',
      '/repos':  'http://localhost:5111',
      '/events': { target: 'http://localhost:5111', changeOrigin: true },
    }
  }
})
