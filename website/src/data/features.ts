/**
 * Six feature labels for the landing page. Blanket-style: a title and an icon,
 * nothing else. Body text was removed to match the GNOME Apps page succinctness.
 */

export interface Feature {
  /** Lucide icon path data (24-unit viewBox). */
  iconPath: string;
  title: string;
}

export const features: Feature[] = [
  {
    // music
    iconPath:
      'M9 18V5l12-2v13 M3 18A3 3 0 1 0 9 18A3 3 0 1 0 3 18Z M15 16A3 3 0 1 0 21 16A3 3 0 1 0 15 16Z',
    title: '14 ambient sounds',
  },
  {
    // bookmark
    iconPath: 'm19 21-7-4-7 4V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z',
    title: 'Save mixes as presets',
  },
  {
    // import / arrow-down-to-line
    iconPath: 'M12 17V3 M6 11l6 6 6-6 M19 21H5',
    title: 'Custom sounds and YouTube URLs',
  },
  {
    // panel-bottom-dashed (taskbar metaphor)
    iconPath:
      'M14 15h1 M19 15h2 M3 15h2 M9 15h1 M3 5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z',
    title: 'Lives in the taskbar',
  },
  {
    // sparkles
    iconPath:
      'M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z M20 3v4 M22 5h-4 M4 17v2 M5 18H3',
    title: 'Light, dark, or follow Windows',
  },
  {
    // heart-handshake
    iconPath:
      'M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.29 1.51 4.04 3 5.5l7 7Z M12 5 9.04 7.96a2.17 2.17 0 0 0 0 3.08v0c.82.82 2.13.85 3 .07l2.07-1.9a2.82 2.82 0 0 1 3.79 0l2.96 2.66 M18 15l-2-2 M15 18l-2-2',
    title: 'Free and open source',
  },
];
