module.exports = {
  content: [
    "./src/**/*.{js,jsx,ts,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        // Existing primary kept but aligned with Design System primary
        primary: {
          DEFAULT: '#006492', // Brand Primary
          light: '#6FB6E8',
          xlight: '#C9E6FF',
          dark: '#3F7FAE',
        },
        // Explicit brand tokens required by design
        background: '#F8F9FF',
        surface: '#FFFFFF',
        'text-primary': '#0E1D2C',
        'text-secondary': '#40484F',
        border: '#D5E4F9',
        error: '#BA1A1A',
        
        success: {
          DEFAULT: '#4CAF8F',
          light: '#E6F7F1',
        },
        warning: {
          DEFAULT: '#F4B64A',
          light: '#FFF4DC',
        },
        error: {
          DEFAULT: '#E76F6F',
          light: '#FDECEC',
        },
        info: {
          DEFAULT: '#5C9EDB',
          light: '#EAF4FF',
        },
        text: {
          primary: '#1F2D3D',
          secondary: '#5F7285',
          disabled: '#AAB7C4',
        },
        border: {
          DEFAULT: '#D7E3EC',
          strong: '#AFC6D8',
        },
        surface: {
          1: '#F7FBFF',
          2: '#FFFFFF',
          3: '#EAF3FA',
        }
      },
      fontFamily: {
        sans: ['Inter', 'sans-serif'],
      },
      container: {
        center: true,
        padding: '1rem',
        screens: {
          sm: '640px',
          md: '768px',
          lg: '1024px',
          xl: '1200px',
        },
      },
      spacing: {
        // Base-8 spacing tokens (values in px)
        1: '4px',
        2: '8px',
        4: '16px',
        6: '24px',
        8: '32px',
        12: '48px',
        16: '64px',
      },
      borderRadius: {
        button: '8px',
        input: '8px',
        card: '16px',
        modal: '16px',
        badge: '9999px',
      },
      boxShadow: {
        card: '0 2px 12px rgba(31,45,61,0.05)',
        hover: '0 8px 24px rgba(31,45,61,0.10)',
      },
      fontSize: {
        'h1': ['32px', { lineHeight: '40px', fontWeight: '700' }],
        'h2': ['24px', { lineHeight: '32px', fontWeight: '600' }],
        'h3': ['20px', { lineHeight: '28px', fontWeight: '600' }],
        'body': ['16px', { lineHeight: '24px', fontWeight: '400' }],
        'body-sm': ['14px', { lineHeight: '20px', fontWeight: '400' }],
        'label': ['12px', { lineHeight: '16px', fontWeight: '500' }],
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
          '0%': { opacity: '0', transform: 'scale(0.95) translateY(10px)' },
          '100%': { opacity: '1', transform: 'scale(1) translateY(0)' },
        },
        shimmer: {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.4' },
        },
        skeletonWave: {
          '0%': { backgroundPosition: '200% 0' },
          '100%': { backgroundPosition: '-200% 0' },
        }
      },
      animation: {
        pageEntrance: 'pageEntrance 0.5s cubic-bezier(0.16, 1, 0.3, 1)',
        slideDown: 'slideDown 0.35s cubic-bezier(0.16, 1, 0.3, 1)',
        modalSlideIn: 'modalSlideIn 0.35s cubic-bezier(0.16, 1, 0.3, 1)',
        shimmer: 'shimmer 1.5s ease-in-out infinite',
        skeletonWave: 'skeletonWave 1.8s ease-in-out infinite',
      }
    },
  },
  plugins: [],
}
