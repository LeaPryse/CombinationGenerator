import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { MonoTypeOperatorFunction, Observable, defer, delay, identity, of, throwError } from 'rxjs';
import {
  N_MAX,
  N_MIN,
  NextResponse,
  PAGE_SIZE_MAX,
  PAGE_SIZE_MIN,
  PageResponse,
  PermutationItem,
  ProblemDetails,
  StartResponse,
} from '../models/permutation.models';
import { SessionIdHolder } from '../session/session-id.holder';
import type { PermutationApi } from './permutation-api';

interface MockSession {
  n: number;
  total: bigint;
  /** Index of the currently displayed permutation; 0 = nothing displayed yet. */
  position: bigint;
  /** "All" mode anchor; null when not in "All" mode. */
  anchor: bigint | null;
}

const NO_MORE = 'No more combinations';

/**
 * In-memory implementation of the API contract for UI development without the server.
 * It emulates the server semantics (sessions, anchor, Back position, errors as ProblemDetails).
 * Development-only: in production every calculation happens on the server.
 */
@Injectable({ providedIn: 'root' })
export class PermutationApiMock implements PermutationApi {
  private readonly session = inject(SessionIdHolder);
  private readonly sessions = new Map<string, MockSession>();
  private counter = 0;

  /** Simulated network latency in ms (0 = synchronous, handy for tests). */
  latencyMs = 150;

  start(n: number): Observable<StartResponse> {
    return this.respond<StartResponse>(() => {
      if (!Number.isInteger(n) || n < N_MIN || n > N_MAX) {
        return this.problem(400, `n must be between ${N_MIN} and ${N_MAX}.`, 'n');
      }
      const sessionId = this.newGuid();
      const total = factorial(n);
      this.sessions.set(sessionId, { n, total, position: 0n, anchor: null });
      return of({ sessionId, n, totalCount: total.toString() });
    });
  }

  next(): Observable<NextResponse> {
    return this.respond<NextResponse>(() => {
      const s = this.currentSession();
      if (!s) return this.sessionNotFound();
      s.anchor = null;
      if (s.position >= s.total) {
        return of({ index: null, permutation: null, hasMore: false, message: NO_MORE });
      }
      s.position += 1n;
      return of({
        index: s.position.toString(),
        permutation: unrank(s.n, s.position - 1n),
        hasMore: s.position < s.total,
        message: null,
      });
    });
  }

  all(pageSize: number, page: string | bigint): Observable<PageResponse> {
    return this.respond<PageResponse>(() => {
      const s = this.currentSession();
      if (!s) return this.sessionNotFound();
      if (!Number.isInteger(pageSize) || pageSize < PAGE_SIZE_MIN || pageSize > PAGE_SIZE_MAX) {
        return this.problem(
          400,
          `pageSize must be between ${PAGE_SIZE_MIN} and ${PAGE_SIZE_MAX}.`,
          'pageSize',
        );
      }
      const pageText = page.toString();
      if (!/^-?\d+$/.test(pageText)) {
        return this.problem(400, 'page must be an integer.', 'page');
      }
      const pageNo = BigInt(pageText);
      if (pageNo < 1n) {
        return this.problem(400, 'page must be greater than or equal to 1.', 'page');
      }

      if (s.anchor === null) s.anchor = s.position;
      const totalItems = s.total - s.anchor;
      const size = BigInt(pageSize);
      const totalPages = (totalItems + size - 1n) / size;
      if (pageNo > 1n && pageNo > totalPages) {
        return this.problem(400, `page must be between 1 and ${totalPages}.`, 'page');
      }

      const items: PermutationItem[] = [];
      const first = s.anchor + (pageNo - 1n) * size + 1n;
      let current: number[] | null = null;
      for (let idx = first; idx <= s.total && idx < first + size; idx++) {
        current = current ? successor(current) : unrank(s.n, idx - 1n);
        items.push({ index: idx.toString(), permutation: [...current] });
      }
      if (items.length > 0) {
        s.position = BigInt(items[items.length - 1].index);
      }

      return of({
        items,
        page: pageNo.toString(),
        pageSize,
        totalPages: totalPages.toString(),
        totalItems: totalItems.toString(),
        firstIndex: totalItems > 0n ? (s.anchor + 1n).toString() : null,
        message: totalItems === 0n ? NO_MORE : null,
      });
    });
  }

  /** Ends "All" mode: clears the anchor and keeps the position (idempotent), like `POST /back`. */
  back(): Observable<void> {
    return this.respond<void>(() => {
      const s = this.currentSession();
      if (!s) return this.sessionNotFound();
      s.anchor = null;
      return of(undefined);
    });
  }

  private respond<T>(factory: () => Observable<T>): Observable<T> {
    const latency: MonoTypeOperatorFunction<T> =
      this.latencyMs > 0 ? delay<T>(this.latencyMs) : identity;
    return defer(factory).pipe(latency);
  }

  private currentSession(): MockSession | undefined {
    const id = this.session.sessionId();
    return id ? this.sessions.get(id) : undefined;
  }

  private sessionNotFound(): Observable<never> {
    const id = this.session.sessionId();
    return throwError(
      () =>
        new HttpErrorResponse({
          status: 404,
          statusText: 'Not Found',
          headers: new HttpHeaders({ 'Content-Type': 'application/problem+json' }),
          error: {
            title: 'Session not found',
            status: 404,
            detail: `Session '${id}' was not found or has expired. Start a new session.`,
          } satisfies ProblemDetails,
        }),
    );
  }

  private problem(status: number, detail: string, field: string): Observable<never> {
    return throwError(
      () =>
        new HttpErrorResponse({
          status,
          statusText: 'Bad Request',
          headers: new HttpHeaders({ 'Content-Type': 'application/problem+json' }),
          error: {
            title: 'One or more validation errors occurred.',
            status,
            detail,
            errors: { [field]: [detail] },
          } satisfies ProblemDetails,
        }),
    );
  }

  private newGuid(): string {
    this.counter++;
    const hex = this.counter.toString(16).padStart(12, '0');
    return `00000000-0000-4000-8000-${hex}`;
  }
}

function factorial(n: number): bigint {
  let result = 1n;
  for (let i = 2n; i <= BigInt(n); i++) result *= i;
  return result;
}

/** Lexicographic unranking of the 0-based `rank` among permutations of 1..n. */
function unrank(n: number, rank: bigint): number[] {
  const pool = Array.from({ length: n }, (_, i) => i + 1);
  const result: number[] = [];
  let r = rank;
  for (let k = n; k >= 1; k--) {
    const f = factorial(k - 1);
    const pos = Number(r / f); // pos < k <= 20, safe as number
    r %= f;
    result.push(pool.splice(pos, 1)[0]);
  }
  return result;
}

/** Classic next-permutation step (Narayana). Assumes a successor exists. */
function successor(perm: number[]): number[] {
  const a = [...perm];
  let i = a.length - 2;
  while (i >= 0 && a[i] >= a[i + 1]) i--;
  if (i < 0) return a;
  let j = a.length - 1;
  while (a[j] <= a[i]) j--;
  [a[i], a[j]] = [a[j], a[i]];
  for (let l = i + 1, r = a.length - 1; l < r; l++, r--) [a[l], a[r]] = [a[r], a[l]];
  return a;
}
