#!/usr/bin/env python3
"""Black-box acceptance checks for the compiled console, using literal expectations."""
import copy
import json
import os
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
DOTNET = os.environ.get("DOTNET_EXE", "dotnet")
DLL = ROOT / "src/TelecomFinance.Console/bin/Release/net10.0/TelecomFinance.Console.dll"
checks = []


def run(*args, code=0):
    result = subprocess.run([DOTNET, str(DLL), *map(str, args)], cwd=ROOT,
                            capture_output=True, text=True)
    assert result.returncode == code, (args, result.returncode, result.stdout, result.stderr)
    return json.loads(result.stdout)


def passed(name):
    checks.append({"name": name, "passed": True})


expected = [
    ("plan-a.json", [-70000, -40000, -10000], 90000, -10000, 150000, 70000),
    ("plan-b.json", [5000, 35000, 65000], 90000, 65000, 75000, 0),
    ("plan-c.json", [80000, 110000, 140000], 90000, 140000, 0, 0),
    ("no-clients.json", [-20000, -90000, -160000], -210000, -160000, 0, 160000),
    ("zero-cash.json", [0, 0, 0], 0, 0, 75000, 0),
    ("varied-months.json", [10000, 13000, 38800], 28800, 38800, 0, 0),
]
for filename, cash, profit, final_cash, debt, funding in expected:
    output = run("forecast", ROOT / "examples" / filename, "--json")
    assert [m["ClosingCash"] for m in output["Months"]] == cash
    assert list(output["Summary"].values()) == [profit, final_cash, debt, funding]
    assert output["StartPeriod"] == "2026-11-01"
    if filename.startswith("plan-"):
        assert all(m["BillableMinutes"] == 20000 for m in output["Months"])
        assert all(m["CallRevenue"] == 100000 and m["SubscriptionRevenue"] == 50000
                   and m["Revenue"] == 150000 and m["ContributionMargin"] == 100000
                   and m["TotalExpenses"] == 120000 and m["Profit"] == 30000
                   for m in output["Months"])
    passed("forecast: " + filename)

comparison = run("compare", "examples/plan-a.json", "examples/plan-b.json", "--json")
assert comparison["Left"]["Summary"]["RequiredFinancing"] == 70000
assert comparison["Right"]["Summary"]["FinalCash"] == 65000
passed("compare: independent plans")

error = run("forecast", "examples/invalid-percent.json", "--json", code=2)
assert error["Error"]["Field"] == "Months[2].PaymentPercent"
passed("reject: invalid payment percentage")

basis = json.loads((ROOT / "examples/plan-b.json").read_text())
with tempfile.TemporaryDirectory() as temporary:
    path = Path(temporary) / "input.json"
    invalid = [
        ("InitialCash", -1, "InitialCash"),
        ("Name", "  ", "Name"),
        ("StartMonth", 13, "StartMonth"),
    ]
    for field, value, expected_field in invalid:
        plan = copy.deepcopy(basis)
        plan[field] = value
        path.write_text(json.dumps(plan))
        error = run("forecast", path, "--json", code=2)
        assert error["Error"]["Field"] == expected_field
        passed("reject: " + field)
    for field, value in [("ActiveClients", 1.5), ("PaymentPercent", 50.5),
                         ("AvgBillableMinutesPerClient", 1.5)]:
        plan = copy.deepcopy(basis)
        plan["Months"][0][field] = value
        path.write_text(json.dumps(plan))
        error = run("forecast", path, "--json", code=2)
        assert field in error["Error"]["Field"]
        passed("reject: fractional " + field)
    for label, plan in [("unknown field", dict(basis, Typo=10)),
                        ("missing field", {k: v for k, v in basis.items() if k != "Months"})]:
        path.write_text(json.dumps(plan))
        assert run("forecast", path, "--json", code=2)["Error"]["Message"]
        passed("reject: " + label)
    path.write_text(json.dumps(basis)[:-1] + ',"Name":"duplicate"}')
    assert run("forecast", path, "--json", code=2)["Error"]["Field"] == "Input.Name"
    passed("reject: duplicate JSON key")

assert run("compare", "examples/plan-a.json", "examples/plan-a.json", "--json", code=2)["Error"]["Field"] == "Plans"
passed("reject: comparison with itself")
assert run("forecast", "examples/not-found.json", "--json", code=2)["Error"]["Field"] == "InputFile"
passed("reject: missing file")

report = {"total": len(checks), "passed": len(checks), "failed": 0, "checks": checks}
target = ROOT / "docs/test-results/console-checks.json"
target.parent.mkdir(parents=True, exist_ok=True)
target.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
print(f"Console checks: {len(checks)} passed; 0 failed")
