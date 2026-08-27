---
name: squad-kit
description: Use when the user wants to set up, configure, or run the squad-kit SDD (spec-driven development) workflow from chat instead of a terminal — "set up squad-kit", "init SDD", "configure the tracker/planner", "new story", "create a story for TICKET-123", "generate a plan from an intake", "squad-plan", "squad status", "squad doctor". Works whether squad-kit is installed as a project dependency or not (falls back to `npx` automatically), so it also runs in a repo that has never used squad-kit before. Gathers agent type, tracker type + credentials, and planner provider + API key conversationally instead of interactive terminal prompts.
license: MIT
version: 0.2.0
---

# squad-kit — chat-driven SDD workflow

squad-kit is a 3-step SDD CLI: **raw story → good plan → implementation** (`.squad/stories/` → `.squad/plans/`). This
skill lets a team member drive that whole workflow — environment setup, story creation, plan generation — from an
agent chat, without ever opening a terminal or knowing the CLI exists.

It works in **two modes**, chosen automatically, never by the user:

| Mode | When | How commands run |
| --- | --- | --- |
| **Package mode** | `squad-kit` is already installed (local `node_modules/.bin/squad` or a global install) in the target project | Runs the real installed `squad` binary |
| **Standalone mode** | Not installed anywhere | Runs the exact same CLI transiently via `npx --yes squad-kit@latest`, no install added to the project |

Both modes call the identical, maintained squad-kit code — standalone mode is not a reimplementation, so there is
nothing here to drift out of sync with the real package.

## 0. Resolve how to invoke squad-kit (do this once per session, before any other step)

Run the resolver script that ships next to this file (path is relative to this `SKILL.md`'s own directory —
resolve it to an absolute path first, e.g. `.claude/skills/squad-kit/scripts/resolve-squad.mjs`):

```
node <skill-dir>/scripts/resolve-squad.mjs
```

It prints JSON: `{"mode": "local" | "npx-remote" | "unavailable", "version": "...", "prefix": "..."}`.

- `mode: "local"` → package mode. Use `prefix` (`npx --no-install squad`) for every command below.
- `mode: "npx-remote"` → standalone mode. Use `prefix` (`npx --yes squad-kit@latest`) for every command below.
  Tell the user once, briefly, that no project dependency was added.
- `mode: "unavailable"` → no network and no local install. Stop and tell the user: Node 18+ and either a local
  squad-kit install or internet access (for `npx`) are required.

Everywhere below, `$SQUAD` means "the `prefix` string you resolved here."

## 1. Setup / init flow

Trigger: "set up squad-kit", "init SDD", "configure squad-kit for this repo", or `.squad/config.yaml` doesn't exist
yet when the user asks for a story/plan.

### 1a. Gather inputs conversationally

Ask (batch into one message or use `AskUserQuestion` for the multiple-choice ones — never ask one-at-a-time):

1. **Agent type(s)** — which slash-command sets to scaffold: `claude-code`, `cursor`, `copilot`, `gemini` (multi-select;
   default to `claude-code` if the user is clearly already in Claude Code and doesn't care).
2. **Tracker type** — `none`, `jira`, `azure` (Azure DevOps Services), or `github` (Issues). If not `none`:
   - Jira: host (e.g. `mycompany.atlassian.net`), account email, API token.
   - Azure: organization, project, PAT.
   - GitHub: PAT (and host only if GitHub Enterprise Server).
3. **Direct planner** (optional) — provider `anthropic` | `openai` | `google`, or skip. **Explain clearly**: this
   is optional and only needed for `squad new-plan --api` / terminal use outside an agent chat — when this skill
   itself drafts a plan (§3), it uses the current conversation and spends no extra API budget.
   - If `openai` or `google`: ask for the API key (API-key-only providers, unchanged).
   - If `anthropic`: ask how to authenticate — **Claude subscription** (Pro/Max/Team, no per-token API bill;
     default/recommended) or **API key**. See §1c for how each is wired up; either choice, or skipping the
     question, is fine — the CLI's default `auto` mode picks a Claude login if one is set up and otherwise
     falls back to an API key.
4. **Project basics** — project name (default: repo folder name), primary language, whether filenames should
   include the tracker id (`naming.includeTrackerId`).

Never echo the API token/PAT back in chat once given, and never write it into any file except `.squad/secrets.yaml`
(§1c).

### 1b. Scaffold config + folders (no secrets yet)

Run, using `$SQUAD` from step 0:

```
$SQUAD init --agents <comma-list> --tracker <type> [--tracker-workspace <jiraHost-or-azureOrg>] \
  [--tracker-project <azureProject>] --name "<name>" --language "<lang>" \
  [--planner <provider> | --no-planner] --skip-secrets-prompt -y
```

This writes `.squad/config.yaml`, the folder skeleton, the bundled prompts reference, and the agent slash-command
files — everything except secrets (which the CLI would otherwise ask for interactively; `--skip-secrets-prompt`
avoids that non-TTY hang).

If `.squad/config.yaml` already exists and the user wants to reconfigure one section only, use
`$SQUAD config set tracker` / `$SQUAD config set planner` equivalents instead — same idea, but pass only the
relevant flags (`--type`, `--provider`) plus `-y`, then still do §1c for the credential itself.

### 1c. Wire up credentials (the piece the CLI can't take non-interactively)

If the user chose **Claude subscription** for the Anthropic planner in §1a: this skill has no terminal, so it
cannot run the browser login itself. Ask the user to run `claude setup-token` themselves (in their own terminal,
outside this chat — it needs their own browser/Claude login) and paste back the resulting token, then run:

```
$SQUAD auth login --token <token> -y
```

This stores the token in `.squad/secrets.yaml` (`planner.anthropicOauthToken`, `0600` on POSIX) and sets
`planner.auth.anthropic: subscription` in `.squad/config.yaml` automatically — no config flag needed. Never echo
the token back once given, and never write it anywhere else. If the user doesn't have a token handy yet, skip
this for now; `auto` mode (the default) will just fall back to an API key or no planner until they run it later.

For everything else — tracker credentials, and the Anthropic API-key path if that's what the user chose instead
of a subscription — build the merged JSON yourself:

1. If `.squad/secrets.yaml` already exists, read it first (plain YAML, small) so you don't clobber unrelated
   providers.
2. Merge in the values gathered in §1a into this exact shape (all fields optional, omit anything not set):

```json
{
  "planner": { "anthropic": "...", "openai": "...", "google": "..." },
  "tracker": {
    "jira": { "host": "...", "email": "...", "token": "..." },
    "azure": { "organization": "...", "project": "...", "pat": "..." },
    "github": { "host": "...", "pat": "..." }
  }
}
```

3. Write it with the bundled script (never hand-write YAML for secrets — this script also sets `0600` permissions
   on POSIX and never prints the values you pass it):

```
node <skill-dir>/scripts/write-secrets.mjs .squad '<json-from-step-2>'
```

4. Confirm to the user that credentials were saved to `.squad/secrets.yaml` (git-ignored by `squad init`) and
   masked — never repeat the token back.

### 1d. Confirm

Run `$SQUAD status` and `$SQUAD doctor` (read-only) and report the summary back to the user in plain language
(tracker/planner configured, any warnings). `status` shows a `planner auth` row (e.g. `subscription · Claude
login (macOS Keychain)`, or `missing — run squad auth login ...`); `doctor` includes `planner auth mode` and
`planner auth vs. runtime` checks — surface anything they flag. Do not run `doctor --fix` without asking first.

## 2. New story flow

Trigger: "create a story", "new story for <feature>", "add a story for TICKET-123".

1. Ask for: feature slug (kebab-case), optional tracker id (skip if `tracker.type` is `none`), optional title hint.
2. Run:

```
$SQUAD new-story <feature-slug> [--id <id>] [--title "<title>"] -y
```

   When a tracker id is given and credentials are configured, this auto-fetches title/description/labels/attachments
   from Jira/Azure/GitHub into the intake — no extra work needed from you.
3. Report the created path (`.squad/stories/<feature>/<id-or-slug>/intake.md`). If the tracker fetch was skipped
   (no id, or `tracker.type: none`), tell the user to fill in the intake by hand — title, description, acceptance
   criteria — before generating a plan.

## 3. Generate-a-plan flow

Trigger: "generate a plan", "squad-plan", "plan this story", or the user hands you an intake file path directly.

1. Resolve the intake path (ask which story if ambiguous — `.squad/stories/**/intake.md`).
2. Run the CLI in **copy mode** so it composes the real meta-prompt for you instead of spending a separate API call:

```
$SQUAD new-plan <intake-path> --copy --no-clipboard -y
```

   This writes the fully composed prompt to `.squad/.last-copy-prompt.md` (the same `generate-plan.md` meta-prompt
   squad-kit ships, with the intake content already merged in — nothing to hand-maintain here).
3. Read `.squad/.last-copy-prompt.md` in full and follow it exactly as your own operating instructions: it already
   specifies the output path (`.squad/plans/<feature>/NN-story-<id>.md`), the `00-overview.md` update, and the
   `00-index.md` update for new features. **You are the planner for this turn** — draft the plan yourself using the
   current conversation's model; do not call an external API for this unless the user explicitly asked for `--api`
   mode (see step 4).
4. Only if the user explicitly wants the *configured direct planner* to draft it (e.g. to match exactly what
   teammates get from the terminal, or to use a specific model tier) — run `$SQUAD new-plan <intake-path> --api -y`
   instead of steps 2-3, and report the resulting plan path.
5. Report the path(s) written and a one-line summary. Remind the user: open a **fresh** agent chat and attach only
   the plan file to implement it cheaply.

## 4. Everything else

- `$SQUAD list [--feature <slug>]`, `$SQUAD status`, `$SQUAD doctor [--json]`, and
  `$SQUAD auth status [--json] [--offline]` are always safe to run read-only for status questions.
- Deletions (`$SQUAD rm story|plan|feature`) and `$SQUAD migrate` are destructive — confirm with the user in chat
  before running them, and prefer `--dry-run` first.
- `$SQUAD auth logout` removes only the Claude login token squad-kit itself stored (never the user's OS/Claude
  Code login) — still confirm with the user first, since it can silently drop the planner back to `auto`'s next
  fallback (an API key, or none).
- Never run `$SQUAD upgrade` without asking (it changes the installed package/version).

## Security notes

- Tokens/PATs/API keys/OAuth tokens (including the Claude subscription token from `squad auth login`) live only
  in `.squad/secrets.yaml` (via §1c) or as env vars the user already set — never in `.squad/config.yaml`, never in
  chat history beyond the single message the user typed them in, never in a plan/story file.
- `.squad/secrets.yaml` is git-ignored by `squad init`; if you ever see it staged in a git status, warn the user
  before they commit.
