import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { sessionInterceptor } from '../core/interceptors/session.interceptor';
import { NextResponse, PageResponse, StartResponse } from '../core/models/permutation.models';
import { PermutationStore } from './permutation.store';

/**
 * Scenario tests: the assignment flow (n = 3) driven through the real store, the real HTTP API service and the
 * real session interceptor. Every request is answered by a small contract-faithful fake of the server
 * (docs/API_CONTRACT.md), so the test also checks what the client SENDS (header, query parameters).
 */

const BASE = '/api/permutations';
const NO_MORE = 'No more combinations';
const N3 = [
  [1, 2, 3],
  [1, 3, 2],
  [2, 1, 3],
  [2, 3, 1],
  [3, 1, 2],
  [3, 2, 1],
];

/** Server-side state of one session, per the contract. Positions are 1-based; 0 = nothing displayed. */
interface FakeSession {
  position: number;
  anchor: number | null;
}

class FakeServer {
  private readonly sessions = new Map<string, FakeSession>();
  private counter = 0;
  readonly log: { url: string; sessionHeader: string | null; params: string }[] = [];

  constructor(private readonly http: HttpTestingController) {}

  /** Answers exactly one pending request. */
  respond(): void {
    const req = this.http.expectOne(() => true);
    this.log.push({
      url: req.request.url,
      sessionHeader: req.request.headers.get('X-Session-Id'),
      params: req.request.params.toString(),
    });
    this.handle(req);
  }

  private handle(req: TestRequest): void {
    const url = req.request.url;
    if (url === `${BASE}/start`) {
      const id = `00000000-0000-0000-0000-00000000000${++this.counter}`;
      this.sessions.set(id, { position: 0, anchor: null });
      const body: StartResponse = { sessionId: id, n: req.request.body.n, totalCount: '6' };
      req.flush(body);
      return;
    }

    const id = req.request.headers.get('X-Session-Id');
    if (!id) {
      req.flush({ status: 400, title: 'One or more validation errors occurred.' }, { status: 400, statusText: 'Bad Request' });
      return;
    }
    const s = this.sessions.get(id);
    if (!s) {
      req.flush({ status: 404, title: 'Session not found' }, { status: 404, statusText: 'Not Found' });
      return;
    }

    if (url === `${BASE}/next`) {
      s.anchor = null;
      let body: NextResponse;
      if (s.position >= 6) {
        body = { index: null, permutation: null, hasMore: false, message: NO_MORE };
      } else {
        s.position++;
        body = { index: String(s.position), permutation: N3[s.position - 1], hasMore: s.position < 6, message: null };
      }
      req.flush(body);
      return;
    }

    if (url === `${BASE}/all`) {
      const pageSize = Number(req.request.params.get('pageSize') ?? '50');
      const page = Number(req.request.params.get('page') ?? '1');
      const anchor = s.anchor ?? s.position;
      const totalItems = 6 - anchor;
      const totalPages = Math.ceil(totalItems / pageSize);
      if (page < 1 || (page > totalPages && page !== 1)) {
        req.flush({ status: 400, detail: `page must be between 1 and ${Math.max(1, totalPages)}.` }, { status: 400, statusText: 'Bad Request' });
        return;
      }
      s.anchor = anchor;
      const first = anchor + (page - 1) * pageSize + 1;
      const items = [];
      for (let i = first; i <= Math.min(6, first + pageSize - 1); i++) {
        items.push({ index: String(i), permutation: N3[i - 1] });
      }
      if (items.length > 0) s.position = Number(items[items.length - 1].index);
      const body: PageResponse = {
        items,
        page: String(page),
        pageSize,
        totalPages: String(totalPages),
        totalItems: String(totalItems),
        firstIndex: totalItems === 0 ? null : String(anchor + 1),
        message: totalItems === 0 ? NO_MORE : null,
      };
      req.flush(body);
      return;
    }

    if (url === `${BASE}/back` && req.request.method === 'POST') {
      s.anchor = null; // ends "All" mode, position unchanged
      req.flush(null, { status: 204, statusText: 'No Content' });
      return;
    }

    throw new Error(`Unexpected request ${url}`);
  }
}

describe('PermutationStore – assignment scenario (n = 3)', () => {
  let store: PermutationStore;
  let http: HttpTestingController;
  let server: FakeServer;

  /** Starts a store action, answers its single request with the fake server, awaits the action. */
  async function act(action: () => Promise<void>): Promise<void> {
    const done = action();
    server.respond();
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
    server = new FakeServer(http);
  });

  afterEach(() => http.verify());

  it('runs the full flow: Next, Next, All, Back, Next, exhaustion, Reset, start again', async () => {
    // Start
    await act(() => store.start(3));
    expect(store.mode()).toBe('single');
    expect(store.totalCount()).toBe('6');
    expect(store.current()).toBeNull();
    expect(server.log[0].sessionHeader).withContext('start must not send a session header').toBeNull();
    const firstSession = store.sessionId();

    // Next → #1 [1,2,3]; Next → #2 [1,3,2]
    await act(() => store.next());
    expect(store.current()).toEqual({ index: '1', permutation: [1, 2, 3] });
    await act(() => store.next());
    expect(store.current()).toEqual({ index: '2', permutation: [1, 3, 2] });
    expect(server.log[1].sessionHeader).toBe(firstSession);

    // All with page size 2 → page 1 starts at #3
    await store.setPageSize(2);
    await act(() => store.showAll());
    expect(store.mode()).toBe('all');
    expect(server.log[3].params).toContain('pageSize=2');
    expect(server.log[3].params).toContain('page=1');
    expect(store.items().map((i) => i.index)).toEqual(['3', '4']);
    expect(store.totalPages()).toBe('2');
    expect(store.totalItems()).toBe('4');

    // Back → POST /back (with session header, no page request); current is the last item of the viewed page (#4)
    await act(() => store.back());
    const backCall = server.log[server.log.length - 1];
    expect(backCall.url).toBe(`${BASE}/back`);
    expect(backCall.sessionHeader).toBe(firstSession);
    http.expectNone(() => true);
    expect(store.mode()).toBe('single');
    expect(store.current()).toEqual({ index: '4', permutation: [2, 3, 1] });

    // Next continues after #4
    await act(() => store.next());
    expect(store.current()).toEqual({ index: '5', permutation: [3, 1, 2] });
    await act(() => store.next());
    expect(store.current()).toEqual({ index: '6', permutation: [3, 2, 1] });

    // Exhaustion → message, current stays #6
    await act(() => store.next());
    expect(store.noMoreMessage()).toBe(NO_MORE);
    expect(store.current()).toEqual({ index: '6', permutation: [3, 2, 1] });
    expect(store.error()).toBeNull();

    // Reset → input screen; start again → new session, new header, fresh state
    store.reset();
    expect(store.mode()).toBe('input');
    expect(store.sessionId()).toBeNull();
    await act(() => store.start(3));
    const secondSession = store.sessionId();
    expect(secondSession).not.toBe(firstSession);
    expect(store.noMoreMessage()).toBeNull();
    expect(store.current()).toBeNull();
    await act(() => store.next());
    expect(server.log[server.log.length - 1].sessionHeader).toBe(secondSession);
    expect(store.current()).toEqual({ index: '1', permutation: [1, 2, 3] });
  });

  it('Back from the last page, then Next shows "No more combinations"', async () => {
    await act(() => store.start(3));
    await act(() => store.next());
    await act(() => store.next());
    await store.setPageSize(2);
    await act(() => store.showAll());
    await act(() => store.goToPage('2'));
    expect(store.page()).toBe('2');
    expect(store.items().map((i) => i.index)).toEqual(['5', '6']);

    await act(() => store.back());
    expect(store.current()?.index).toBe('6');
    await act(() => store.next());

    expect(store.noMoreMessage()).toBe(NO_MORE);
    expect(store.current()?.index).toBe('6');
  });

  it('paging back to page 1 after page 2 makes Back show the last item of page 1', async () => {
    await act(() => store.start(3));
    await store.setPageSize(2);
    await act(() => store.showAll());
    await act(() => store.goToPage('2'));
    await act(() => store.goToPage('1'));

    await act(() => store.back());

    expect(store.current()?.index).toBe('2');
    await act(() => store.next());
    expect(store.current()?.index).toBe('3');
  });

  it('All when nothing remains shows the message; Back keeps the current item', async () => {
    await act(() => store.start(3));
    for (let i = 0; i < 6; i++) await act(() => store.next());

    await act(() => store.showAll());

    expect(store.mode()).toBe('all');
    expect(store.items()).toEqual([]);
    expect(store.noMoreMessage()).toBe(NO_MORE);
    expect(store.hasPages()).toBeFalse();
    await act(() => store.back());
    expect(store.current()?.index).toBe('6');
    expect(store.noMoreMessage()).toBeNull();
  });

  it('entering All again after Back + Next starts after the newly displayed item', async () => {
    await act(() => store.start(3));
    await store.setPageSize(2);
    await act(() => store.showAll()); // #1,#2
    await act(() => store.back());
    await act(() => store.next()); // #3

    await act(() => store.showAll());

    expect(store.page()).toBe('1');
    expect(store.items().map((i) => i.index)).toEqual(['4', '5']);
  });

  it('Back, then All again starts right after the shown item (contract "Back, then All" table)', async () => {
    await act(() => store.start(3));
    await store.setPageSize(2);
    await act(() => store.next()); // step 1: #1
    expect(store.current()?.index).toBe('1');

    await act(() => store.showAll()); // step 2: #2,#3
    expect(store.items().map((i) => i.index)).toEqual(['2', '3']);
    expect(store.totalItems()).toBe('5');
    await act(() => store.goToPage('2')); // step 3: #4,#5
    expect(store.items().map((i) => i.index)).toEqual(['4', '5']);

    await act(() => store.back()); // step 4: 204, client shows #5
    expect(store.mode()).toBe('single');
    expect(store.current()).toEqual({ index: '5', permutation: [3, 1, 2] });

    await act(() => store.showAll()); // step 5: #6 only
    expect(store.mode()).toBe('all');
    expect(store.items()).toEqual([{ index: '6', permutation: [3, 2, 1] }]);
    expect(store.totalItems()).toBe('1');
    expect(store.totalPages()).toBe('1');
    expect(server.log.map((l) => l.url.replace(BASE, ''))).toEqual([
      '/start',
      '/next',
      '/all',
      '/all',
      '/back',
      '/all',
    ]);
  });

  it('a failed Back keeps the All view; retrying Back then All still starts after the shown item', async () => {
    await act(() => store.start(3));
    await store.setPageSize(2);
    await act(() => store.next()); // #1
    await act(() => store.showAll()); // #2,#3

    const failed = store.back();
    http
      .expectOne(`${BASE}/back`)
      .flush({ status: 500, detail: 'Boom' }, { status: 500, statusText: 'Internal Server Error' });
    await failed;
    expect(store.mode()).toBe('all');
    expect(store.items().map((i) => i.index)).toEqual(['2', '3']);
    expect(store.error()).toBe('Boom');

    await act(() => store.back());
    expect(store.current()?.index).toBe('3');
    await act(() => store.showAll());
    expect(store.items().map((i) => i.index)).toEqual(['4', '5']);
  });

  it('a session that expired before Back returns to the input screen', async () => {
    await act(() => store.start(3));
    await act(() => store.showAll());
    (server as unknown as { sessions: Map<string, unknown> }).sessions.clear();

    await act(() => store.back());

    expect(store.mode()).toBe('input');
    expect(store.sessionId()).toBeNull();
    expect(store.error()).not.toBeNull();
  });

  it('a session that expired mid-flow returns to the input screen', async () => {
    await act(() => store.start(3));
    await act(() => store.next());
    // Simulate server-side expiry: a different store-independent session id is unknown to the fake server.
    (server as unknown as { sessions: Map<string, unknown> }).sessions.clear();

    await act(() => store.next());

    expect(store.mode()).toBe('input');
    expect(store.sessionId()).toBeNull();
    expect(store.error()).not.toBeNull();
  });
});
