/** Zero-volume sounds are absent from the map, as in the Windows app. */

export interface Preset {
  name: string
  mix: Record<string, number>
}

export const presets: Preset[] = [
  {
    name: "Rainy café",
    mix: { rain: 0.5, "coffee-shop": 0.45, fireplace: 0.2 },
  },
  { name: "Night train", mix: { train: 0.55, rain: 0.3, wind: 0.2 } },
  {
    name: "Seaside evening",
    mix: { waves: 0.55, wind: 0.2, "summer-night": 0.35 },
  },
]
