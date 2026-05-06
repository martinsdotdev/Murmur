/**
 * Six features for the landing page. The redesign gives each a title plus a
 * one-line subtitle (the earlier title-only rows grew a hint line).
 *
 * Icons come from @hugeicons/core-free-icons, the same set the app imports, so
 * the two halves of the site draw from one vocabulary.
 */
import {
  Bookmark01Icon,
  ComputerIcon,
  ContrastIcon,
  FavouriteIcon,
  SlidersVerticalIcon,
  Upload04Icon,
} from "@hugeicons/core-free-icons"
import type { IconSvgElement } from "@hugeicons/react"

export interface Feature {
  icon: IconSvgElement
  title: string
  sub: string
}

export const features: Feature[] = [
  {
    icon: SlidersVerticalIcon,
    title: "Layer sounds at any volume",
    sub: "A storm over a coffee shop",
  },
  {
    icon: Bookmark01Icon,
    title: "Save blends as presets",
    sub: "Every slider back where you left it",
  },
  {
    icon: Upload04Icon,
    title: "Bring your own audio",
    sub: "An audio file, or a YouTube link",
  },
  {
    icon: ComputerIcon,
    title: "Closes to the tray, keeps playing",
    sub: "Your media keys still work, and it can launch at startup",
  },
  {
    icon: ContrastIcon,
    title: "Follows your system theme",
    sub: "No flashbang at 2am",
  },
  {
    icon: FavouriteIcon,
    title: "Free, GPLv3, no telemetry",
    sub: "No account, no ads",
  },
]
