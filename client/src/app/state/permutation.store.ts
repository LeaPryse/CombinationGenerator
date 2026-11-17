import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, firstValueFrom, map } from 'rxjs';
import { PERMUTATION_API } from '../core/api/permutation-api';
import {
  BigIntString,
  DEFAULT_PAGE_SIZE,
  PAGE_SIZE_MAX,
  PAGE_SIZE_MIN,
  PermutationItem,
  ProblemDetails,
} from '../core/models/permutation.models';
import { SessionIdHolder } from '../core/session/session-id.holder';

export type GeneratorMode = 'input' | 'single' | 'all';

export const SESSION_EXPIRED_MESSAGE =
  'Your session has expired or was not found. Please enter n to start again.';
const NETWORK_ERROR_MESSAGE = 'Cannot reach the server. Please try again.';
const GENERIC_ERROR_MESSAGE = 'Something went wrong. Please try again.';

/**
 * Single source of truth for the generator screen.
 *
 * All big numbers (totalCount, indices, page, totalPages, totalItems) are kept as decimal strings,
 * exactly as received from the server; BigInt is used only for comparisons/arithmetic.
 */
@Injectable({ providedIn: 'root' })
export class PermutationStore {
  private readonly api = inject(PERMUTATION_API);
  private readonly session = inject(SessionIdHolder);

  private readonly _n = signal<number | null>(null);
  private readonly _totalCount = signal<BigIntString | null>(null);
  private readonly _mode = signal<GeneratorMode>('input');
  private readonly _current = signal<PermutationItem | null>(null);
  private readonly _page = signal<BigIntString>('1');
  private readonly _pageSize = signal<number>(DEFAULT_PAGE_SIZE);
  private readonly _totalPages = signal<BigIntString>('0');
  private readonly _totalItems = signal<BigIntString>('0');
  private readonly _items = signal<PermutationItem[]>([]);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _noMoreMessage = signal<string | null>(null);

  /** Incremented on every request and on reset, so stale responses are ignored. */
  private requestSeq = 0;

  readonly n = this._n.asReadonly();
  readonly sessionId = this.session.sessionId;
  readonly totalCount = this._totalCount.asReadonly();
  readonly mode = this._mode.asReadonly();
  readonly current = this._current.asReadonly();
  readonly page = this._page.asReadonly();
  readonly pageSize = this._pageSize.asReadonly();
  readonly totalPages = this._totalPages.asReadonly();
  readonly totalItems = this._totalItems.asReadonly();
  readonly items = this._items.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly noMoreMessage = this._noMoreMessage.asReadonly();

  readonly hasSession = computed(() => this.sessionId() !== null);
  readonly hasPages = computed(() => BigInt(this._totalPages()) > 0n);

  async start(n: number): Promise<void> {
    if (this._loading()) return;
    const res = await this.run(this.api.start(n));
    if (!res) return;
    this.session.set(res.sessionId);
    this._n.set(res.n);
    this._totalCount.set(res.totalCount);
    this._current.set(null);
    this.clearPaging();
    this._noMoreMessage.set(null);
    this._mode.set('single');
  }

  async next(): Promise<void> {
    if (this._loading() || !this.hasSession()) return;
    const res = await this.run(this.api.next());
    if (!res) return;
    this._mode.set('single');
    if (res.index !== null && res.permutation !== null) {
      this._current.set({ index: res.index, permutation: res.permutation });
      this._noMoreMessage.set(null);
    } else {
      this._noMoreMessage.set(res.message);
    }
  }

  /** Enters "All" mode: page 1 starts right after the currently displayed permutation. */
  showAll(): Promise<void> {
    return this.loadPage('1');
  }

  /** Navigates to a page (decimal string or BigInt, may exceed 2^53). Ignores invalid pages. */
  goToPage(page: BigIntString | bigint): Promise<void> {
    const text = page.toString().trim();
    if (!/^\d+$/.test(text)) return Promise.resolve();
    const target = BigInt(text);
    const total = BigInt(this._totalPages());
    if (target < 1n || (target > total && target !== 1n)) return Promise.resolve();
    return this.loadPage(target.toString());
  }

  /**
   * Loads a page of "All" mode. `size` defaults to the current page size; all paging state
   * (including the page size) is committed only on success, so a failure leaves it consistent.
   */
  async loadPage(page: BigIntString, size: number = this._pageSize()): Promise<void> {
    if (this._loading() || !this.hasSession()) return;
    const res = await this.run(this.api.all(size, page));
    if (!res) return;
    this._items.set(res.items);
    this._page.set(res.page);
    this._pageSize.set(res.pageSize);
    this._totalPages.set(res.totalPages);
    this._totalItems.set(res.totalItems);
    this._noMoreMessage.set(res.message);
    this._mode.set('all');
  }

  /**
   * Changes the page size. In "All" mode it reloads page 1 with the new size (the server keeps the
   * anchor fixed) and the size is committed only if that request succeeds.
   */
  async setPageSize(size: number): Promise<void> {
    if (!Number.isInteger(size) || size < PAGE_SIZE_MIN || size > PAGE_SIZE_MAX) return;
    if (this._loading()) return;
    if (this._mode() === 'all') {
      await this.loadPage('1', size);
    } else {
      this._pageSize.set(size);
    }
  }

  /**
   * Leaves "All" mode. Calls `POST /back` so the server clears its paging anchor (a later "All" then
   * starts right after the shown item). The shown item is the last item of the displayed page, which the
   * client already has, so no page is requested. The UI switches only after the call succeeds; on failure
   * the "All" view stays as it was (404 returns to the input screen, like every other action).
   */
  async back(): Promise<void> {
    if (this._loading()) return;
    if (!this.hasSession()) {
      this.applyBack();
      return;
    }
    const ok = await this.run(this.api.back().pipe(map(() => true as const)));
    if (!ok) return;
    this.applyBack();
  }

  private applyBack(): void {
    const items = this._items();
    if (items.length > 0) {
      this._current.set(items[items.length - 1]);
    }
    this.clearPaging();
    this._noMoreMessage.set(null);
    this._error.set(null);
    this._mode.set(this.hasSession() ? 'single' : 'input');
  }

  /** Returns to the input screen. The old server session is abandoned and expires on its own. */
  reset(): void {
    this.requestSeq++;
    this.session.clear();
    this._n.set(null);
    this._totalCount.set(null);
    this._current.set(null);
    this.clearPaging();
    this._loading.set(false);
    this._error.set(null);
    this._noMoreMessage.set(null);
    this._mode.set('input');
  }

  clearError(): void {
    this._error.set(null);
  }

  private clearPaging(): void {
    this._items.set([]);
    this._page.set('1');
    this._totalPages.set('0');
    this._totalItems.set('0');
  }

  /**
   * Runs a request with loading/error handling. Resolves to `null` on error or when the response
   * became stale (e.g. the user pressed Reset meanwhile).
   */
  private async run<T>(request: Observable<T>): Promise<T | null> {
    const seq = ++this.requestSeq;
    this._loading.set(true);
    this._error.set(null);
    try {
      const result = await firstValueFrom(request);
      return seq === this.requestSeq ? result : null;
    } catch (err) {
      if (seq === this.requestSeq) this.handleError(err);
      return null;
    } finally {
      if (seq === this.requestSeq) this._loading.set(false);
    }
  }

  private handleError(err: unknown): void {
    if (err instanceof HttpErrorResponse && err.status === 404) {
      this.reset();
      this._error.set(SESSION_EXPIRED_MESSAGE);
      return;
    }
    this._error.set(toMessage(err));
  }
}

function toMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    if (err.status === 0) return NETWORK_ERROR_MESSAGE;
    const problem = err.error as ProblemDetails | null;
    if (problem && typeof problem === 'object') {
      return problem.detail ?? problem.title ?? GENERIC_ERROR_MESSAGE;
    }
    return GENERIC_ERROR_MESSAGE;
  }
  return GENERIC_ERROR_MESSAGE;
}
