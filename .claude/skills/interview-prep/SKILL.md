---
name: interview-prep
description: How to write docs/INTERVIEW.md for the Combination Generator — answers to the assignment's 5 general questions and the rationale behind every design decision, grounded in the actual code. Load before writing interview or architecture documentation.
---

# Interview Preparation Document

Output: `docs/INTERVIEW.md`, in English. Audience: the candidate, rehearsing to defend the solution in a technical interview, and the interviewer reading it.

## Rules
- **Grounded in code**: every claim links to the file that implements it (relative markdown links, e.g. `[PermutationService.cs](../server/src/...)`). Read the code before writing; never describe behavior that isn't there.
- Each answer: **short answer (1–2 sentences) → explanation → trade-offs / alternatives considered**.
- Plain language; include a tiny example where it helps (n=3).
- No marketing tone, no filler.

## Required sections
1. **Overview** — what the system does, one diagram (Mermaid) of client → API → service → session store.
2. **The 5 assignment questions**
   1. Which state-management model did you choose and why? (server: session-id + `ISessionStore`; client: Signals store)
   2. NextPermutation vs. index-based permutation — difference, and where each is used.
   3. What happens when 3 different users connect simultaneously? Why?
   4. Which 2 server-side changes would add Redis persistence?
   5. What is the main drawback of NextPermutation for n = 20?
3. **Design decisions log** — table: decision | alternatives | why chosen. Must include: `long` vs `BigInteger`, counts as JSON strings, session header vs cookie session, per-session lock, page anchor semantics, "Back" semantics, Clean Architecture layering, Signals vs NgRx, paginator windowing for huge page counts.
4. **Complexity table** — time/memory per endpoint.
5. **Known limitations & next steps** — e.g. in-memory store is single-instance, no auth, session expiry.
6. **Likely follow-up questions** — 8–10 questions an interviewer may ask, each with a short answer.
