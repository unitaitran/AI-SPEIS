module.exports = {
  content: [
    "./src/**/*.{js,jsx,ts,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        primary: {
          DEFAULT: '#6FB6E8',
          dark: '#3F7FAE',
          light: '#B9DCF5',
          xlight: '#EAF6FF',
        },
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
