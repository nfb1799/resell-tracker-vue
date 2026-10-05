import {
  Chart, BarElement, LineElement, PointElement, CategoryScale, LinearScale, Filler, Tooltip,
} from 'chart.js'

// Register only what the two chart components draw, so the rest of Chart.js
// tree-shakes out of their lazy chunk.
Chart.register(BarElement, LineElement, PointElement, CategoryScale, LinearScale, Filler, Tooltip)

// Every token a chart may paint with, resolved to real values by
// useThemeColors. A bar's colour token must be one of these.
export const CHART_TOKENS = [
  'text-dimmed', 'text-primary', 'border-subtle', 'border-color', 'bg-secondary',
  'accent-primary', 'accent-rgb', 'success-color', 'danger-color',
  'depop-color', 'ebay-color', 'vinted-color', 'other-color',
  'chart-1', 'chart-2', 'chart-3', 'chart-4', 'chart-5', 'chart-6',
]

const compact = (v) => (Math.abs(v) >= 1000 ? `${Math.round(v / 100) / 10}k` : v)

// Axis, grid and tooltip styling shared by every chart. `tooltip` supplies the
// title/label/footer callbacks, which differ per chart.
export function baseOptions(c, tooltip) {
  const tick = { color: c['text-dimmed'], font: { size: 11, family: 'Inter' } }
  return {
    responsive: true,
    maintainAspectRatio: false,
    animation: { duration: 400 },
    interaction: { mode: 'index', intersect: false },
    layout: { padding: { top: 6, right: 4 } },
    scales: {
      x: {
        grid: { display: false },
        border: { display: false },
        ticks: { ...tick, maxRotation: 0, autoSkip: true },
      },
      y: {
        grid: { color: c['border-subtle'], drawTicks: false },
        border: { display: false, dash: [3, 3] },
        ticks: { ...tick, padding: 8, maxTicksLimit: 6, callback: compact },
      },
    },
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: c['bg-secondary'],
        borderColor: c['border-color'],
        borderWidth: 1,
        cornerRadius: 10,
        padding: { x: 12, y: 9 },
        titleColor: c['text-primary'],
        bodyColor: c['text-primary'],
        footerColor: c['text-dimmed'],
        titleFont: { size: 12.5, weight: '600', family: 'Inter' },
        bodyFont: { size: 12.5, family: 'Inter' },
        footerFont: { size: 11.5, weight: 'normal', family: 'Inter' },
        displayColors: false,
        caretPadding: 8,
        callbacks: tooltip,
      },
    },
  }
}
