import { cp, mkdir, rm } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const toolsDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(toolsDirectory, '../..');
const angularOutput = resolve(
  repositoryRoot,
  'frontend/dist/apps/site/browser',
);
const sitesOutput = resolve(repositoryRoot, 'dist');

await rm(sitesOutput, { force: true, recursive: true });
await mkdir(resolve(sitesOutput, 'client'), { recursive: true });
await mkdir(resolve(sitesOutput, 'server'), { recursive: true });
await cp(angularOutput, resolve(sitesOutput, 'client'), { recursive: true });
await cp(
  resolve(toolsDirectory, 'sites-worker.mjs'),
  resolve(sitesOutput, 'server/index.js'),
);
