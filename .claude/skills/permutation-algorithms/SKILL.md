---
name: permutation-algorithms
description: Reference for the permutation algorithms used by the Combination Generator — NextPermutation, index-based unranking (factorial number system), limits for n ≤ 20, and the test properties they must satisfy. Load before touching the Domain layer.
---

# Permutation Algorithms

Permutations of `[1..n]` in **lexicographic order**, indexed **0-based internally** and shown **1-based** to the user.
Example n=3: `#1 [1,2,3]`, `#2 [1,3,2]`, `#3 [2,1,3]`, `#4 [2,3,1]`, `#5 [3,1,2]`, `#6 [3,2,1]`.

## Limits
- 1 ≤ n ≤ 20.
- 20! = **2,432,902,008,176,640,000** < `long.MaxValue` (9,223,372,036,854,775,807) → use `long`. 21! overflows — reject n > 20.
- Precompute `Factorials[0..20]` as a static `long[]` once.

## 1. NextPermutation (sequential step) — used by GetNext and within a page
In-place, O(n) worst case, amortized O(1):
```
1. i = n-2; while i >= 0 and a[i] >= a[i+1]: i--
2. if i < 0: return false              // a is the last permutation
3. j = n-1; while a[j] <= a[i]: j--
4. swap(a[i], a[j])
5. reverse(a, i+1, n-1)
6. return true
```

## 2. Unrank (index → permutation) — used to jump to any page
Factorial number system / Lehmer code. O(n²) with a list (fine for n ≤ 20):
```
available = [1..n]
for pos in 0..n-1:
    f = Factorials[n-1-pos]
    k = index / f
    index = index % f
    result[pos] = available[k]; available.RemoveAt(k)
```
Precondition: `0 ≤ index < n!`.

## 3. (Optional) Rank (permutation → index)
Inverse of Unrank — useful for tests and for validating stored state.

## When to use which
| Operation | Algorithm | Why |
|---|---|---|
| "Next" button | NextPermutation from the stored permutation | cheapest per step |
| First item of a page / jump to page p | Unrank(anchor + (p-1)·pageSize) | random access; NextPermutation would need up to ~10^18 steps for n=20 |
| Remaining items of a page | NextPermutation from the first item | O(pageSize · n) |

**Main drawback of NextPermutation for n=20:** it is strictly sequential — reaching index k costs k steps, so random access (last page, page 1,000,000,000) is infeasible; it also requires keeping the current permutation as state.

## Required test properties
- n=3 yields exactly the 6 permutations above, in order.
- For n = 1..8: iterating NextPermutation from identity yields exactly n! distinct permutations, then returns false.
- For n = 1..8, all i: `Unrank(i)` equals the i-th permutation produced by NextPermutation.
- `Next(Unrank(i)) == Unrank(i+1)` for random i with n=20.
- `Rank(Unrank(i)) == i` (if Rank is implemented).
- `Unrank(0)` is identity; `Unrank(n!-1)` is descending.
- n=20: `Unrank(20! - 1)` completes in well under 1 ms; `Factorial(20) == 2432902008176640000`.
- Invalid input: n < 1, n > 20, index < 0, index ≥ n! → `ArgumentOutOfRangeException`.
