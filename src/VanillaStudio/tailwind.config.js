/** @type {import('tailwindcss').Config} */
module.exports = {
  // Scoped to the landing page surface. Scans Razor components for utility usage.
  content: ['./Components/**/*.{razor,html,cs}'],
  theme: {
    extend: {
      colors: {
        ink: '#0D1117',        // dark editor surface (hero, footer)
        panel: '#161B22',      // raised dark panel
        line: '#232A33',       // hairline on dark
        paper: '#F2F4F7',      // cool near-white body
        'paper-2': '#FFFFFF',  // cards on light
        'paper-line': '#E2E7ED', // hairline on light
        steel: {
          DEFAULT: '#3D5A73',  // structure / architecture
          400: '#5B7D99',
          700: '#2C4458',
        },
        signal: {
          DEFAULT: '#FFC24B',  // the single bold accent — generate / live layer
          600: '#E8A21F',
        },
        clay: '#E2795B',       // top slice layer (UI) accent
        dim: '#8B96A5',        // muted text on dark
        'dim-2': '#5A6675',    // muted text on light
      },
      fontFamily: {
        display: ['"Space Grotesk"', 'system-ui', 'sans-serif'],
        sans: ['Inter', 'system-ui', 'sans-serif'],
        mono: ['"JetBrains Mono"', 'ui-monospace', 'SFMono-Regular', 'monospace'],
      },
      letterSpacing: {
        kicker: '0.18em',
      },
      maxWidth: {
        page: '1180px',
      },
    },
  },
  plugins: [],
}
