/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{js,jsx,ts,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        primary: {
          DEFAULT: 'var(--color-primary)',
          hover: 'var(--color-primary-hover)',
          dark: 'var(--color-primary-hover)',
          light: 'var(--color-primary-light)',
          xlight: 'var(--color-primary-xlight)',
        },
        secondary: {
          DEFAULT: 'var(--color-secondary)',
          hover: 'var(--color-secondary-hover)',
          dark: 'var(--color-secondary-hover)',
          light: 'var(--color-secondary-light)',
          xlight: 'var(--color-secondary-xlight)',
        },
        accent: {
          DEFAULT: 'var(--color-accent)',
          light: 'var(--color-accent-light)',
        },
        surface: {
          DEFAULT: 'var(--color-surface)',
          elevated: 'var(--color-surface-elevated)',
          muted: 'var(--color-surface-muted)',
          1: 'var(--color-background)',
          2: 'var(--color-surface)',
          3: 'var(--color-surface-muted)',
        },
        background: 'var(--color-background)',
        border: {
          DEFAULT: 'var(--color-border)',
          strong: 'var(--color-border-strong)',
        },
        text: {
          primary: 'var(--color-text-primary)',
          secondary: 'var(--color-text-secondary)',
          muted: 'var(--color-text-muted)',
          disabled: 'var(--color-text-disabled)',
        },
        success: {
          DEFAULT: 'var(--color-success)',
          light: 'var(--color-success-light)',
        },
        warning: {
          DEFAULT: 'var(--color-warning)',
          light: 'var(--color-warning-light)',
        },
        error: {
          DEFAULT: 'var(--color-error)',
          light: 'var(--color-error-light)',
        },
        info: {
          DEFAULT: 'var(--color-info)',
          light: 'var(--color-info-light)',
        },
      },
      fontFamily: {
        sans: ['Plus Jakarta Sans', 'Inter', 'sans-serif'],
        body: ['Inter', 'Plus Jakarta Sans', 'sans-serif'],
        mono: ['JetBrains Mono', 'Roboto Mono', 'monospace'],
      },
      borderRadius: {
        'sm': 'var(--radius-sm)',
        'DEFAULT': 'var(--radius-md)',
        'md': 'var(--radius-md)',
        'lg': 'var(--radius-lg)',
        'xl': 'var(--radius-xl)',
        '2xl': 'var(--radius-2xl)',
        'full': 'var(--radius-full)',
      },
      boxShadow: {
        'sm': 'var(--shadow-sm)',
        'DEFAULT': 'var(--shadow-md)',
        'md': 'var(--shadow-md)',
        'lg': 'var(--shadow-lg)',
        'xl': 'var(--shadow-xl)',
        'glow-primary': 'var(--shadow-glow-primary)',
        'glow-ai': 'var(--shadow-glow-ai)',
      },
      keyframes: {
        pageEntrance: {
          '0%': { opacity: '0', transform: 'translateY(8px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        slideDown: {
          '0%': { opacity: '0', transform: 'translateY(-12px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        modalSlideIn: {
          '0%': { opacity: '0', transform: 'scale(0.96) translateY(10px)' },
          '100%': { opacity: '1', transform: 'scale(1) translateY(0)' },
        },
        shimmer: {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.4' },
        },
        pulseGlow: {
          '0%, 100%': { boxShadow: '0 0 10px rgba(124, 58, 237, 0.2)' },
          '50%': { boxShadow: '0 0 25px rgba(124, 58, 237, 0.45)' },
        },
        audioWave: {
          '0%, 100%': { height: '8px' },
          '50%': { height: '24px' },
        }
      },
      animation: {
        pageEntrance: 'pageEntrance 0.35s cubic-bezier(0.16, 1, 0.3, 1)',
        slideDown: 'slideDown 0.25s cubic-bezier(0.16, 1, 0.3, 1)',
        modalSlideIn: 'modalSlideIn 0.3s cubic-bezier(0.16, 1, 0.3, 1)',
        shimmer: 'shimmer 1.5s ease-in-out infinite',
        pulseGlow: 'pulseGlow 2s ease-in-out infinite',
        audioWave: 'audioWave 1.2s ease-in-out infinite',
      }
    },
  },
  plugins: [],
}
