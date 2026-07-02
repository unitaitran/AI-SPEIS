import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/setupTests.js',
    css: true,
    include: ['src/**/*.test.{js,jsx,ts,tsx}'],
    exclude: ['src/App.test.js'],
    coverage: {
      reporter: ['text', 'html', 'lcov'],
      exclude: ['src/index.js', 'src/reportWebVitals.js', 'src/setupTests.js', 'src/**/*.css', 'src/locales/**'],
    },
  },
});
