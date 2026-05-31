# MobileIoT

Cross-platform test harness that drives end-to-end tests across phones and a Raspberry Pi from a Mac or Windows dev box. Full reference: [.claude/skills/test-harness/SKILL.md](.claude/skills/test-harness/SKILL.md).

## Testing the harness

Canonical sequence, run from this repo root:

```
python -m harness doctor
python -m harness run --tier hardware
```

**Always run `python -m harness doctor` first.** If it exits non-zero, stop and report which devices or secrets the doctor output names as missing — do **not** invoke `run`. The doctor probe surfaces unplugged phones, missing secrets, and wrong-host iOS / Windows skips before they look like flakes.

**Slash command:** typing `/test-harness` performs the same sequence (doctor → gate → run). Prefer it over running the commands by hand.

**Output is the deliverable.** Both commands stream rich-console output and pytest's report to the terminal. Pass it through to the user — don't suppress, summarize, or paraphrase it. JSON mode (`--json`) exists for CI / aggregation and is documented in [SKILL.md](.claude/skills/test-harness/SKILL.md) — don't use it for ad-hoc runs.

**Device source of truth:** `devices.yaml` (committed) overlaid by `devices.local.yaml` (per-dev, gitignored). Never edit `devices.yaml` to coerce a pass — adjust `devices.local.yaml` instead.

## Repo orientation

- `harness/` — Python test framework (Typer CLI, inventory, transports, integration plugins).
- `src/MobileIoT/` — MAUI + Uno apps. The `TestHarness` build configuration embeds `HarnessHttpHost` on `127.0.0.1:47821`.
- `src/pi/` — Python demos that run on the Raspberry Pi.
- `tools/setup_dev_mac.sh` — bootstraps a fresh macOS dev box (Python, adb, .NET SDK, MAUI workload).
- `harness/runs/` — per-run artifact directories (gitignored).
- `.env.harness` — secrets (gitignored); `.env.harness.example` is the template.

## Don't

- Don't edit `harness/harness/` core when adding an integration — register a plugin via entry-points (see SKILL.md `## Adding a new integration`).
- Don't commit `devices.local.yaml` or `.env.harness`.
- Don't skip the doctor pre-flight before `run --tier hardware`.
- Don't add `--json` to harness commands unless the user explicitly asks.
