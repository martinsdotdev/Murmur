/// <reference types="astro/client" />
/// <reference types="vite-plugin-pwa/react" />

/** Replaced at build time from package.json. See astro.config.mjs. */
declare const __APP_VERSION__: string

/** Chromium only, and still in no spec, so lib.dom does not carry it. */
interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>
  readonly userChoice: Promise<{ outcome: "accepted" | "dismissed" }>
}

interface Window {
  /**
   * Parked by InstallPrompt.astro, because beforeinstallprompt fires once, is
   * never replayed, and can land before the client:only island has parsed.
   */
  __murmurInstallPrompt?: BeforeInstallPromptEvent
}
