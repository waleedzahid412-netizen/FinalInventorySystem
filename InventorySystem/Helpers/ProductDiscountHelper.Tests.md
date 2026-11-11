# ProductDiscountHelper — edge-case test matrix

Run these manually or as unit tests against `ProductDiscountHelper`.

| Case | Input | Expected |
|------|--------|----------|
| No discount | type null | Amount 0, type null |
| Percent 10, Qty 2, Price 100 | apply=true | Amount 20 |
| Percent 15, Qty 1, Price 85 | apply=true | Amount 12.75 |
| Fixed 50, Qty 3 | apply=true | Amount 150 |
| Fixed larger than gross | Qty 1, Price 40, Fixed 50 | Amount 40 (capped) |
| Switch OFF | product has 10% | Amount 0 |
| Percent 0 / 31 normalize | catalog | Validation fail |
| Manual percent 1–30 | IsValidManualPercentage | true for 1..30, false for 0/31/12.5 |
| FREE line | any product discount | Amount 0 (caller skips FREE) |
