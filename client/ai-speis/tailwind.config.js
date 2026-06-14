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
      }
    },
  },
  plugins: [],
}
