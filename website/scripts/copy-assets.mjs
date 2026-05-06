// Mirrors the app's living assets into the site's public folder before dev/build.
// Idempotent, only copies when source is newer than destination.
import { existsSync, mkdirSync, statSync, copyFileSync, readdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const websiteRoot = resolve(here, '..');
const repoRoot = resolve(websiteRoot, '..');
const appRoot = join(repoRoot, 'Murmur');

const targets = [
  {
    src: join(appRoot, 'Assets', 'Source', 'AppIcon.svg'),
    dst: join(websiteRoot, 'public', 'icon.svg'),
  },
  {
    src: join(appRoot, 'Assets', 'AppIcon.ico'),
    dst: join(websiteRoot, 'public', 'favicon.ico'),
  },
];

const soundsSrc = join(appRoot, 'Sounds');
const soundsDst = join(websiteRoot, 'public', 'sounds');

function copyIfStale(src, dst) {
  if (!existsSync(src)) {
    console.warn(`[copy-assets] missing source: ${src}`);
    return;
  }
  const dstDir = dirname(dst);
  if (!existsSync(dstDir)) mkdirSync(dstDir, { recursive: true });
  if (existsSync(dst) && statSync(dst).mtimeMs >= statSync(src).mtimeMs) return;
  copyFileSync(src, dst);
  console.log(`[copy-assets] ${src} → ${dst}`);
}

for (const { src, dst } of targets) copyIfStale(src, dst);

if (existsSync(soundsSrc)) {
  for (const file of readdirSync(soundsSrc)) {
    if (!file.endsWith('.ogg')) continue;
    copyIfStale(join(soundsSrc, file), join(soundsDst, file));
  }
} else {
  console.warn(`[copy-assets] missing sounds dir: ${soundsSrc}`);
}
