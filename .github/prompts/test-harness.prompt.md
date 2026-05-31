---
mode: agent
description: Run the MobileIoT test harness (doctor pre-flight, then hardware tier)
---

You are running the MobileIoT test harness for the user. Execute these steps literally, in order. Do not skip steps. Do not parse JSON.

1. **Run doctor.** In the terminal, from the repo root, run:

   ```
   python -m harness doctor
   ```

   Show me the full output in the chat. Do not summarize it.

2. **Check the exit code.** If doctor's exit code is **non-zero**, stop here. Tell me — in one short paragraph — which devices are offline or which secrets are missing. Read the names directly from the doctor output. Do not proceed to step 3.

3. **Run the hardware tier.** If doctor exited 0, run in the same terminal:

   ```
   python -m harness run --tier hardware
   ```

   Show me the full output in the chat. Do not add `--json`. Do not suppress or summarize the output.

4. **Headline.** After the run finishes, say in one sentence whether the harness passed (exit code 0) or failed (non-zero).

Rules:

- The terminal output is the deliverable. Pass it through verbatim.
- Do not interpret pytest results beyond pass/fail. The user reads the report themselves.
- Do not run any other commands. No git, no builds, no edits.
- This command is safe to re-run. Each invocation produces a fresh `harness/runs/<run-id>/` directory.
