import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { sessionInterceptor } from '../core/interceptors/session.interceptor';
import { PageResponse } from '../core/models/permutation.models';
import { PermutationStore, SESSION_EXPIRED_MESSAGE } from './permutation.store';

const SID = '8f6c2a3e-4b1d-4c8e-9f2a-1d3b5e7a9c01';
const BASE = '/api/permutations';

function page(partial: Partial<PageResponse>): PageResponse {
  return {
    items: [],
    page: '1',
    pageSize: 2,
    totalPages: '0',
    totalItems: '0',
    firstIndex: null,
    message: null,
    ...partial,
  };
}

describe('PermutationStore', () => {
  let store: PermutationStore;
  let http: HttpTestingController;

  async function start(n = 3, totalCount = '6'): Promise<void> {
    const done = store.start(n);
    http.expectOne(`${BASE}/start`).flush({ sessionId: SID, n, totalCount });
    await done;
  }

  async function next(index: string | null, permutation: number[] | null): Promise<void> {
    const done = store.next();
    const req = http.expectOne(`${BASE}/next`);
    expect(req.request.headers.get('X-Session-Id')).toBe(SID);
    req.flush({
      index,
      permutation,
      hasMore: index !== null,
      message: index === null ? 'No more combinations' : null,
    });
    await done;
  }

  async function loadAll(pageNo: string, response: PageResponse): Promise<void> {
    const done = pageNo === '1' && store.mode() !== 'all' ? store.showAll() : store.goToPage(pageNo);
    const req = http.expectOne((r) => r.url === `${BASE}/all`);
    expect(req.request.params.get('page')).toBe(pageNo);
    req.flush(response);
    await done;
  }

  /** Runs store.back() and answers its POST /back with 204. */
  async function back(): Promise<void> {
    const done = store.back();
    const req = http.expectOne(`${BASE}/back`);
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('X-Session-Id')).toBe(SID);
    req.flush(null, { status: 204, statusText: 'No Content' });
    await done;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([sessionInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    store = TestBed.inject(PermutationStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts in input mode with empty state', () => {
    expect(store.mode()).toBe('input');
    expect(store.sessionId()).toBeNull();
    expect(store.totalCount()).toBeNull();
    expect(store.current()).toBeNull();
    expect(store.loading()).toBeFalse();
  });

  it('start stores session, n and totalCount (as string) and switches to single mode', async () => {
    await start(20, '2432902008176640000');
    expect(store.sessionId()).toBe(SID);
    expect(store.n()).toBe(20);
    expect(store.totalCount()).toBe('2432902008176640000');
    expect(store.mode()).toBe('single');
  });

  it('sets loading while a request is in flight', async () => {
    const done = store.start(3);
    expect(store.loading()).toBeTrue();
    http.expectOne(`${BASE}/start`).flush({ sessionId: SID, n: 3, totalCount: '6' });
    await done;
    expect(store.loading()).toBeFalse();
  });

  it('next sets the current permutation', async () => {
    await start();
    await next('1', [1, 2, 3]);
    expect(store.current()).toEqual({ index: '1', permutation: [1, 2, 3] });
    expect(store.noMoreMessage()).toBeNull();
  });

  it('next with no more combinations keeps the current item and sets the message', async () => {
    await start(1, '1');
    await next('1', [1]);
    await next(null, null);
    expect(store.current()).toEqual({ index: '1', permutation: [1] });
    expect(store.noMoreMessage()).toBe('No more combinations');
  });

  it('showAll loads page 1 with the current page size and enters all mode', async () => {
    await start();
    await next('1', [1, 2, 3]);
    await next('2', [1, 3, 2]);
    await loadAll(
      '1',
      page({
        items: [
          { index: '3', permutation: [2, 1, 3] },
          { index: '4', permutation: [2, 3, 1] },
        ],
        totalPages: '2',
        totalItems: '4',
        firstIndex: '3',
      }),
    );
    expect(store.mode()).toBe('all');
    expect(store.items().length).toBe(2);
    expect(store.page()).toBe('1');
    expect(store.totalPages()).toBe('2');
    expect(store.hasPages()).toBeTrue();
  });

  it('back shows the last item of the displayed page and returns to single mode', async () => {
    await start();
    await loadAll(
      '1',
      page({
        items: [
          { index: '1', permutation: [1, 2, 3] },
          { index: '2', permutation: [1, 3, 2] },
        ],
        totalPages: '3',
        totalItems: '6',
        firstIndex: '1',
      }),
    );
    await loadAll(
      '2',
      page({
        page: '2',
        items: [
          { index: '3', permutation: [2, 1, 3] },
          { index: '4', permutation: [2, 3, 1] },
        ],
        totalPages: '3',
        totalItems: '6',
        firstIndex: '1',
      }),
    );
    await back();
    expect(store.mode()).toBe('single');
    expect(store.current()).toEqual({ index: '4', permutation: [2, 3, 1] });
    expect(store.items()).toEqual([]);
    http.expectNone((r) => r.url === `${BASE}/all`);
  });

  it('back keeps the All view until POST /back succeeds', async () => {
    await start();
    const items = [
      { index: '1', permutation: [1, 2, 3] },
      { index: '2', permutation: [1, 3, 2] },
    ];
    await loadAll('1', page({ items, totalPages: '3', totalItems: '6', firstIndex: '1' }));
    const done = store.back();
    expect(store.loading()).toBeTrue();
    expect(store.mode()).toBe('all');
    expect(store.items()).toEqual(items);
    http.expectOne(`${BASE}/back`).flush(null, { status: 204, statusText: 'No Content' });
    await done;
    expect(store.loading()).toBeFalse();
    expect(store.mode()).toBe('single');
    expect(store.current()).toEqual(items[1]);
  });

  it('back failing with 500 stays in All mode with the paging intact and shows the detail', async () => {
    await start();
    await next('1', [1, 2, 3]);
    const items = [
      { index: '2', permutation: [1, 3, 2] },
      { index: '3', permutation: [2, 1, 3] },
    ];
    await loadAll('1', page({ items, totalPages: '3', totalItems: '5', firstIndex: '2' }));
    const done = store.back();
    http
      .expectOne(`${BASE}/back`)
      .flush({ title: 'Server error', detail: 'Boom' }, { status: 500, statusText: 'Internal Server Error' });
    await done;
    expect(store.mode()).toBe('all');
    expect(store.items()).toEqual(items);
    expect(store.page()).toBe('1');
    expect(store.totalPages()).toBe('3');
    expect(store.current()).toEqual({ index: '1', permutation: [1, 2, 3] });
    expect(store.error()).toBe('Boom');
    expect(store.loading()).toBeFalse();
  });

  it('back failing with 404 returns to the input screen with the session-expired message', async () => {
    await start();
    await loadAll('1', page({ items: [{ index: '1', permutation: [1, 2, 3] }], totalPages: '6' }));
    const done = store.back();
    http
      .expectOne(`${BASE}/back`)
      .flush({ title: 'Session not found', status: 404 }, { status: 404, statusText: 'Not Found' });
    await done;
    expect(store.mode()).toBe('input');
    expect(store.sessionId()).toBeNull();
    expect(store.error()).toBe(SESSION_EXPIRED_MESSAGE);
  });

  it('ignores a /back response that arrives after reset', async () => {
    await start();
    await loadAll('1', page({ items: [{ index: '1', permutation: [1, 2, 3] }], totalPages: '6' }));
    const done = store.back();
    const req = http.expectOne(`${BASE}/back`);
    store.reset();
    req.flush(null, { status: 204, statusText: 'No Content' });
    await done;
    expect(store.mode()).toBe('input');
    expect(store.current()).toBeNull();
  });

  it('back is ignored while another request is in flight', async () => {
    await start();
    await loadAll('1', page({ items: [{ index: '1', permutation: [1, 2, 3] }], totalPages: '6' }));
    const pending = store.goToPage('2');
    await store.back();
    http.expectNone(`${BASE}/back`);
    http
      .expectOne((r) => r.url === `${BASE}/all`)
      .flush(page({ page: '2', items: [{ index: '2', permutation: [1, 3, 2] }], totalPages: '6' }));
    await pending;
    expect(store.mode()).toBe('all');
  });

  it('back without a session switches to input without a request', async () => {
    await store.back();
    http.expectNone(`${BASE}/back`);
    expect(store.mode()).toBe('input');
  });

  it('back after an empty page keeps the current item', async () => {
    await start(1, '1');
    await next('1', [1]);
    await loadAll('1', page({ message: 'No more combinations' }));
    expect(store.noMoreMessage()).toBe('No more combinations');
    expect(store.hasPages()).toBeFalse();
    await back();
    expect(store.current()).toEqual({ index: '1', permutation: [1] });
  });

  it('goToPage sends pages beyond 2^53 as exact strings', async () => {
    await start(20, '2432902008176640000');
    await loadAll(
      '1',
      page({
        pageSize: 1,
        items: [{ index: '1', permutation: [] }],
        totalPages: '2432902008176640000',
        totalItems: '2432902008176640000',
        firstIndex: '1',
      }),
    );
    await loadAll(
      '2432902008176640000',
      page({
        page: '2432902008176640000',
        pageSize: 1,
        items: [{ index: '2432902008176640000', permutation: [] }],
        totalPages: '2432902008176640000',
        totalItems: '2432902008176640000',
        firstIndex: '1',
      }),
    );
    expect(store.page()).toBe('2432902008176640000');
  });

  it('goToPage ignores invalid or out-of-range pages', async () => {
    await start();
    await loadAll('1', page({ items: [{ index: '1', permutation: [1, 2, 3] }], totalPages: '6' }));
    await store.goToPage('0');
    await store.goToPage('7');
    await store.goToPage('abc');
    http.expectNone((r) => r.url === `${BASE}/all`);
  });

  it('setPageSize reloads page 1 in all mode with the new size', async () => {
    await start();
    await loadAll('1', page({ items: [{ index: '1', permutation: [1, 2, 3] }], totalPages: '3' }));
    const done = store.setPageSize(5);
    const req = http.expectOne((r) => r.url === `${BASE}/all`);
    expect(req.request.params.get('pageSize')).toBe('5');
    expect(req.request.params.get('page')).toBe('1');
    req.flush(page({ pageSize: 5, items: [{ index: '1', permutation: [1, 2, 3] }], totalPages: '2' }));
    await done;
    expect(store.pageSize()).toBe(5);
  });

  it('setPageSize keeps the previous size, items and totalPages when the reload fails', async () => {
    await start();
    const items = [
      { index: '1', permutation: [1, 2, 3] },
      { index: '2', permutation: [1, 3, 2] },
    ];
    await loadAll('1', page({ items, totalPages: '3', totalItems: '6' }));
    const previousSize = store.pageSize();
    const done = store.setPageSize(5);
    const req = http.expectOne((r) => r.url === `${BASE}/all`);
    expect(req.request.params.get('pageSize')).toBe('5');
    expect(store.pageSize()).toBe(previousSize);
    req.flush(
      { title: 'Server error', detail: 'Boom' },
      { status: 500, statusText: 'Internal Server Error' },
    );
    await done;
    expect(store.pageSize()).toBe(previousSize);
    expect(store.items()).toEqual(items);
    expect(store.totalPages()).toBe('3');
    expect(store.page()).toBe('1');
    expect(store.mode()).toBe('all');
    expect(store.error()).toBe('Boom');
  });

  it('setPageSize outside all mode only stores the size', async () => {
    await start();
    await store.setPageSize(5);
    http.expectNone((r) => r.url === `${BASE}/all`);
    expect(store.pageSize()).toBe(5);
  });

  it('reset clears everything and returns to input mode', async () => {
    await start();
    await next('1', [1, 2, 3]);
    store.reset();
    expect(store.mode()).toBe('input');
    expect(store.sessionId()).toBeNull();
    expect(store.current()).toBeNull();
    expect(store.totalCount()).toBeNull();
  });

  it('ignores a response that arrives after reset', async () => {
    await start();
    const done = store.next();
    const req = http.expectOne(`${BASE}/next`);
    store.reset();
    req.flush({ index: '1', permutation: [1, 2, 3], hasMore: true, message: null });
    await done;
    expect(store.current()).toBeNull();
    expect(store.mode()).toBe('input');
  });

  it('a 404 returns to the input screen with a session-expired message', async () => {
    await start();
    const done = store.next();
    http
      .expectOne(`${BASE}/next`)
      .flush({ title: 'Session not found', status: 404 }, { status: 404, statusText: 'Not Found' });
    await done;
    expect(store.mode()).toBe('input');
    expect(store.sessionId()).toBeNull();
    expect(store.error()).toBe(SESSION_EXPIRED_MESSAGE);
    expect(store.loading()).toBeFalse();
  });

  it('a 400 shows the ProblemDetails detail and keeps the state', async () => {
    const done = store.start(3);
    http
      .expectOne(`${BASE}/start`)
      .flush(
        { title: 'One or more validation errors occurred.', detail: 'n must be between 1 and 20.' },
        { status: 400, statusText: 'Bad Request' },
      );
    await done;
    expect(store.error()).toBe('n must be between 1 and 20.');
    expect(store.mode()).toBe('input');
    store.clearError();
    expect(store.error()).toBeNull();
  });

  it('a network failure shows a connectivity message', async () => {
    const done = store.start(3);
    http.expectOne(`${BASE}/start`).error(new ProgressEvent('error'), { status: 0 });
    await done;
    expect(store.error()).toContain('Cannot reach the server');
  });
});
