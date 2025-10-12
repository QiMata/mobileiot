from .manual_test_helper import manual_test  # noqa: F401


def pytest_addoption(parser):
    parser.addoption(
        "--run-manual",
        action="store_true",
        default=False,
        help="Execute manual hardware-in-the-loop validation tests.",
    )


def pytest_configure(config):
    config.addinivalue_line("markers", "manual: marks tests that require manual interaction with hardware")
