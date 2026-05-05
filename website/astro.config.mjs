import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';

export default defineConfig({
  // `site` includes the base path so sitemap URLs are valid for the project-page
  // deployment at /Murmur. Astro 6 uses `site` verbatim as the prefix for
  // sitemap entries; without /Murmur, every entry would 404.
  site: 'https://martinsdotdev.github.io/Murmur',
  base: '/Murmur',
  trailingSlash: 'never',
  integrations: [sitemap()],
  build: {
    inlineStylesheets: 'auto',
  },
});
