---
description: Run the MobileIoT test harness (doctor pre-flight, then hardware tier; output streams to terminal).
---

Execute this sequence and report the result. The user wants the test harness exercised against the connected hardware, with the terminal output visible.

## Steps

1. **Confirm cwd is the repo root.** Verify `devices.yaml` and `harness/pyproject.toml` both exist in the current directory. If not, `cd` to the MobileIoT repo root before proceeding.

2. **Doctor pre-flight.** Run:

   ```
   python -m harness doctor
   ```

   Show the output verbatim. Note the exit code.

3. **Gate.** If doctor exited non-zero, stop. Read the missing devices / missing secrets directly from the doctor output and name them to the user. Suggest the concrete fix (plug in / authorize the device, or populate `.env.harness` from `.env.harness.example`). Do **not** run step 4.

4. **Hardware tier.** If doctor exited 0, run:

   ```
   python -m harness run --tier hardware
   ```

   Show the output verbatim — both the rich-console events and pytest's report. Do not add `--json`. Do not suppress or summarize.

5. **Headline.** After the run finishes, output a single line stating pass (exit 0) or fail (non-zero), plus the path to the run artifacts (the most recent directory under `harness/runs/`).

## Rules

- The terminal output IS the deliverable — pass it through, don't paraphrase.
- Don't interpret pytest results beyond pass/fail.
- Re-runnable: each invocation creates a fresh `harness/runs/<run-id>/` directory.
- JSON mode (`--json`) is for CI and large-model aggregation; not used here. See [SKILL.md](../skills/test-harness/SKILL.md) if the user explicitly asks for JSON.

## Reference

Full CLI surface, exit-code conventions, and plugin authoring: [.claude/skills/test-harness/SKILL.md](../skills/test-harness/SKILL.md). Repo entry pointer: [../../CLAUDE.md](../../CLAUDE.md).
