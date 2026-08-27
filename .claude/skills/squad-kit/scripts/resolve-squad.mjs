#!/usr/bin/env node
// Resolves how to invoke the squad-kit CLI in the current project, without ever
// requiring the project to declare squad-kit as a dependency.
//
// Tries, in order:
//   1. A locally/globally installed `squad` binary (via `npx --no-install`, which
//      checks node_modules/.bin then the global bin — no network involved).
//   2. A transient run via `npx --yes squad-kit@latest` (downloads/caches on demand).
//
// Prints one JSON line: { mode, version, prefix }
//   mode:   "local" | "npx-remote" | "unavailable"
//   prefix: the command prefix the skill should prepend to every squad-kit
//           invocation, e.g. "npx --no-install squad" or "npx --yes squad-kit@latest"
import { spawnSync } from 'node:child_process';

function tryVersion(cmd, args) {
  const res = spawnSync(cmd, args, {
    encoding: 'utf8',
    shell: process.platform === 'win32',
  });
  if (res.status === 0 && res.stdout) {
    return res.stdout.trim().split('\n')[0];
  }
  return null;
}

const localVersion = tryVersion('npx', ['--no-install', 'squad', '--version']);
if (localVersion) {
  console.log(
    JSON.stringify({ mode: 'local', version: localVersion, prefix: 'npx --no-install squad' }, null, 2),
  );
  process.exit(0);
}

const remoteVersion = tryVersion('npx', ['--yes', 'squad-kit@latest', '--version']);
console.log(
  JSON.stringify(
    {
      mode: remoteVersion ? 'npx-remote' : 'unavailable',
      version: remoteVersion,
      prefix: 'npx --yes squad-kit@latest',
    },
    null,
    2,
  ),
);
process.exit(remoteVersion ? 0 : 1);
