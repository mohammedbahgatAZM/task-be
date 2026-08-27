#!/usr/bin/env node
// Writes (overwrites) `<squadDir>/secrets.yaml` from a JSON payload, matching
// squad-kit's own `SquadSecrets` shape and file permissions (0600 on POSIX).
//
// This script does NOT read or merge the existing file — by design, so it never
// has to parse arbitrary YAML. The caller (the squad-kit skill) is responsible
// for reading the existing secrets.yaml first (if present) and including any
// values that should survive in the JSON payload it passes here.
//
// Usage:
//   node write-secrets.mjs <squadDir> '<json-payload>'
//
// json-payload shape (all keys optional, omit anything unset):
//   {
//     "planner": { "anthropic": "...", "openai": "...", "google": "..." },
//     "tracker": {
//       "jira":   { "host": "...", "email": "...", "token": "..." },
//       "azure":  { "organization": "...", "project": "...", "pat": "..." },
//       "github": { "host": "...", "pat": "..." }
//     }
//   }
//
// `planner.anthropic` here is an Anthropic API key only. A Claude subscription (OAuth) token is a
// different mechanism with its own side effects (it also flips `planner.auth.anthropic` to
// `subscription` in config.yaml) — that goes through `squad auth login --token <value>`, not this
// script. See SKILL.md §1c.
//
// Never logs the payload or the values it contains.
import fs from 'node:fs';
import path from 'node:path';

const [, , squadDirArg, payloadArg] = process.argv;

if (!squadDirArg || !payloadArg) {
  console.error('Usage: node write-secrets.mjs <squadDir> <json-payload>');
  process.exit(1);
}

let payload;
try {
  payload = JSON.parse(payloadArg);
} catch (err) {
  console.error(`Invalid JSON payload: ${err.message}`);
  process.exit(1);
}

if (payload === null || typeof payload !== 'object' || Array.isArray(payload)) {
  console.error('JSON payload must be an object.');
  process.exit(1);
}

const BARE_SAFE = /^[A-Za-z0-9._@+/=-]+$/;

function dumpScalar(value) {
  const s = String(value);
  if (s.length > 0 && BARE_SAFE.test(s)) return s;
  return JSON.stringify(s); // valid YAML double-quoted scalar
}

function isPlainObject(v) {
  return v !== null && typeof v === 'object' && !Array.isArray(v);
}

function dumpBlock(obj, indent) {
  let out = '';
  const pad = '  '.repeat(indent);
  for (const [key, value] of Object.entries(obj)) {
    if (value === undefined || value === null || value === '') continue;
    if (isPlainObject(value)) {
      const nested = dumpBlock(value, indent + 1);
      if (!nested) continue; // drop empty nested objects entirely
      out += `${pad}${key}:\n${nested}`;
    } else {
      out += `${pad}${key}: ${dumpScalar(value)}\n`;
    }
  }
  return out;
}

const body = dumpBlock(payload, 0);
const secretsDir = path.resolve(squadDirArg);
const secretsFile = path.join(secretsDir, 'secrets.yaml');

fs.mkdirSync(secretsDir, { recursive: true });
fs.writeFileSync(secretsFile, body, 'utf8');

if (process.platform !== 'win32') {
  try {
    fs.chmodSync(secretsFile, 0o600);
  } catch {
    // best-effort; some filesystems refuse chmod
  }
}

console.log(`Wrote ${secretsFile} (values not shown).`);
