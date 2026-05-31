# MobileIoT — Copilot instructions

This is the MobileIoT cross-platform test harness. The Python framework lives in `harness/`; the in-app HTTP host lives in `src/MobileIoT/` under the `TestHarness` build configuration. The harness drives end-to-end tests across Android, iOS, Windows, and a Raspberry Pi from a Mac or Windows dev box.

## Testing the harness

When the user says "test the harness" (or uses the `/test-harness` chat prompt), run **exactly** these two commands in the terminal, from the repo root:

```
python -m harness doctor
```

Wait for it to finish. **If the exit code is non-zero, stop. Tell the user which devices are offline or which secrets are missing — read the names directly from the doctor output.** Do not run the second command.

If doctor exited 0, then run:

```
python -m harness run --tier hardware
```

The output of both commands is what the user wants to see. Show it in the chat in full. **Do not suppress, summarize, parse, or paraphrase it. Pass it through.** After the run finishes, say in one sentence whether it passed (exit 0) or failed.

Do **not** add `--json` to either command. JSON mode is for CI and is documented in `.claude/skills/test-harness/SKILL.md` — don't use it for ad-hoc runs.

## Repo orientation

- `harness/` — Python test framework (Typer CLI, inventory, transports, integration plugins).
- `src/MobileIoT/` — MAUI + Uno apps. The `TestHarness` build configuration embeds `HarnessHttpHost` on `127.0.0.1:47821`.
- `src/pi/` — Python demos that run on the Raspberry Pi.
- `tools/setup_dev_mac.sh` — bootstraps a fresh macOS dev box.
- `devices.yaml` — committed device inventory; `devices.local.yaml` is the per-dev override (gitignored).
- `harness/runs/` — per-run artifact directories (gitignored).
- `.env.harness` — secrets (gitignored); `.env.harness.example` is the template.

## Don't

- Don't edit anything under `harness/harness/` when adding an integration — the framework auto-discovers plugins via entry-points. See `.claude/skills/test-harness/SKILL.md` § "Adding a new integration".
- Don't commit `devices.local.yaml` or `.env.harness`.
- Don't add `--json` to harness commands unless the user explicitly asks.
- Don't skip the doctor pre-flight before `run --tier hardware`.

## Deeper reference

For the full CLI surface (`build`, `repl`, per-integration runs, JSON mode), build configurations, transports, and plugin authoring, read `.claude/skills/test-harness/SKILL.md`.
