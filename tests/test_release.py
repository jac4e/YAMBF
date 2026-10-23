from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "ci"))
from validate_release import validate


class ReleaseValidationTests(unittest.TestCase):
    def test_matching_stable_versions(self):
        for version in ("0.0.0", "1.0.1", "12.34.56"):
            with self.subTest(version=version):
                self.assertEqual(validate("v" + version, version), version)

    def test_rejects_mismatched_package(self):
        with self.assertRaises(ValueError):
            validate("v1.0.2", "1.0.1")

    def test_rejects_nonstable_or_malformed_tags(self):
        for tag in ("1.0.1", "v1.0", "v1.0.1.2", "v1.0.1-beta", "v1.0.1+build",
                    "v01.0.1", "v1.00.1", "v1.0.01", "v1.a.1", "v1.0.1\n", "v1.0.1/other"):
            with self.subTest(tag=tag):
                with self.assertRaises(ValueError):
                    validate(tag, "1.0.1")
