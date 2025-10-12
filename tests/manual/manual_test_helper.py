from __future__ import annotations

import os
import textwrap
from dataclasses import dataclass
from typing import List, Optional

import pytest

MANUAL_ENV_FLAG = "RUN_MANUAL_TESTS"


@dataclass
class ManualStep:
    """Represents a single manual validation step."""

    description: str


@dataclass
class ManualTestPlan:
    """Container for the metadata shared across manual tests."""

    identifier: str
    title: str
    objective: str
    steps: List[ManualStep]
    expected_result: str
    references: Optional[List[str]] = None

    def format_instructions(self) -> str:
        bullet_steps = "\n".join(f"  {idx}. {step.description}" for idx, step in enumerate(self.steps, start=1))
        references = "\n".join(f"   - {ref}" for ref in self.references or [])
        references_block = f"\nReferences:\n{references}" if references else ""
        return textwrap.dedent(
            f"""
            Manual test {self.identifier}: {self.title}
            Objective: {self.objective}

            Steps:
            {bullet_steps}

            Expected result:
              {self.expected_result}{references_block}
            """
        ).strip()


def should_run_manual_tests(request: pytest.FixtureRequest) -> bool:
    """Determines whether manual tests should execute."""

    if os.getenv(MANUAL_ENV_FLAG) in {"1", "true", "True"}:
        return True

    run_option = False
    if hasattr(request.config, "getoption"):
        try:
            run_option = bool(request.config.getoption("--run-manual"))
        except (ValueError, AttributeError):
            run_option = False
    return run_option


class ManualTestExecutor:
    """Utility that renders instructions and collects user confirmation."""

    def execute(self, plan: ManualTestPlan) -> None:
        instructions = plan.format_instructions()
        print("\n" + "=" * 80)
        print(instructions)
        print("=" * 80 + "\n")
        confirmation = input("Type PASS to confirm the expected result was observed (anything else fails): ").strip()
        if confirmation.lower() not in {"pass", "p", "y", "yes"}:
            pytest.fail(f"Manual verification failed for {plan.identifier}")


@pytest.fixture
def manual_test(request: pytest.FixtureRequest) -> ManualTestExecutor:
    """Fixture that controls whether manual tests execute."""

    if not should_run_manual_tests(request):
        pytest.skip(
            "Manual hardware validation skipped. Set RUN_MANUAL_TESTS=1 or pass --run-manual to run these tests."
        )
    return ManualTestExecutor()
