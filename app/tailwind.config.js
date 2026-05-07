/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        gh: {
          bg:      'var(--gh-bg)',
          surface: 'var(--gh-surface)',
          card:    'var(--gh-card)',
          border:  'var(--gh-border)',
          text:    'var(--gh-text)',
          muted:   'var(--gh-muted)',
          accent:  'var(--gh-accent)',
        }
      }
    }
  },
  plugins: [
    require('@tailwindcss/typography'),
  ]
}
