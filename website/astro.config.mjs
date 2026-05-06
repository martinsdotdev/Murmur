import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';

export default defineConfig({
  // Custom domain (murmur.umaru.dev). Pages serves at the apex of the subdomain
  // so `base` defaults to '/'. The public/CNAME file tells Pages to bind this
  // domain to the deployment.
  site: 'https://murmur.umaru.dev',
  trailingSlash: 'never',
  integrations: [sitemap()],
  build: {
    inlineStylesheets: 'auto',
  },
});
