import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { SessionIdHolder } from '../session/session-id.holder';
import { PermutationApiMock } from './permutation-api.mock';

describe('PermutationApiMock', () => {
  let api: PermutationApiMock;
  let holder: SessionIdHolder;

  async function startSession(n: number): Promise<void> {
    const res = await firstValueFrom(api.start(n));
    holder.set(res.sessionId);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({});
    api = TestBed.inject(PermutationApiMock);
    api.latencyMs = 0;
    holder = TestBed.inject(SessionIdHolder);
  });

  it('start returns n! as a string, also for n = 20', async () => {
    expect((await firstValueFrom(api.start(3))).totalCount).toBe('6');
    expect((await firstValueFrom(api.start(20))).totalCount).toBe('2432902008176640000');
  });

  it('start rejects n outside 1..20 with a 400 ProblemDetails', async () => {
    const err = await firstValueFrom(api.start(21)).catch((e: HttpErrorResponse) => e);
    expect((err as HttpErrorResponse).status).toBe(400);
    expect((err as HttpErrorResponse).error.detail).toBe('n must be between 1 and 20.');
  });

  it('next walks the n = 3 sequence and then reports no more combinations', async () => {
    await startSession(3);
    const expected = [
      [1, 2, 3],
      [1, 3, 2],
      [2, 1, 3],
      [2, 3, 1],
      [3, 1, 2],
      [3, 2, 1],
    ];
    for (let i = 0; i < expected.length; i++) {
      const res = await firstValueFrom(api.next());
      expect(res.index).toBe(String(i + 1));
      expect(res.permutation).toEqual(expected[i]);
      expect(res.hasMore).toBe(i < expected.length - 1);
    }
    const end = await firstValueFrom(api.next());
    expect(end).toEqual({
      index: null,
      permutation: null,
      hasMore: false,
      message: 'No more combinations',
    });
  });

  it('all pages after the anchor and moves the position to the last returned item', async () => {
    await startSession(3);
    await firstValueFrom(api.next());
    await firstValueFrom(api.next()); // #2 displayed

    const p1 = await firstValueFrom(api.all(2, '1'));
    expect(p1.items.map((i) => i.index)).toEqual(['3', '4']);
    expect(p1.totalPages).toBe('2');
    expect(p1.totalItems).toBe('4');
    expect(p1.firstIndex).toBe('3');

    const p2 = await firstValueFrom(api.all(2, '2'));
    expect(p2.items).toEqual([
      { index: '5', permutation: [3, 1, 2] },
      { index: '6', permutation: [3, 2, 1] },
    ]);
    expect(p2.firstIndex).toBe('3');

    const after = await firstValueFrom(api.next());
    expect(after.message).toBe('No more combinations');
  });

  it('all returns an empty page with a message when nothing follows the anchor', async () => {
    await startSession(1);
    await firstValueFrom(api.next());
    const page = await firstValueFrom(api.all(2, '1'));
    expect(page.items).toEqual([]);
    expect(page.totalPages).toBe('0');
    expect(page.firstIndex).toBeNull();
    expect(page.message).toBe('No more combinations');
  });

  it('all addresses pages beyond 2^53 for n = 20', async () => {
    await startSession(20);
    const page = await firstValueFrom(api.all(1, '2432902008176640000'));
    expect(page.items).toEqual([
      {
        index: '2432902008176640000',
        permutation: [20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1],
      },
    ]);
  });

  it('all rejects a page beyond totalPages', async () => {
    await startSession(3);
    const err = await firstValueFrom(api.all(2, '4')).catch((e: HttpErrorResponse) => e);
    expect((err as HttpErrorResponse).status).toBe(400);
  });

  it('returns 404 for an unknown session', async () => {
    holder.set('11111111-1111-1111-1111-111111111111');
    const err = await firstValueFrom(api.next()).catch((e: HttpErrorResponse) => e);
    expect((err as HttpErrorResponse).status).toBe(404);
  });

  it('back clears the anchor so the next all starts right after the shown item (contract table)', async () => {
    await startSession(3);
    expect((await firstValueFrom(api.next())).index).toBe('1');
    const p1 = await firstValueFrom(api.all(2, '1'));
    expect(p1.items.map((i) => i.index)).toEqual(['2', '3']);
    expect(p1.firstIndex).toBe('2');
    expect(p1.totalItems).toBe('5');
    const p2 = await firstValueFrom(api.all(2, '2'));
    expect(p2.items.map((i) => i.index)).toEqual(['4', '5']);

    await firstValueFrom(api.back());

    const again = await firstValueFrom(api.all(2, '1'));
    expect(again.items).toEqual([{ index: '6', permutation: [3, 2, 1] }]);
    expect(again.firstIndex).toBe('6');
    expect(again.totalItems).toBe('1');
    expect(again.totalPages).toBe('1');
  });

  it('without back a second all reuses the stale anchor', async () => {
    await startSession(3);
    await firstValueFrom(api.next());
    await firstValueFrom(api.all(2, '1'));
    await firstValueFrom(api.all(2, '2'));
    const again = await firstValueFrom(api.all(2, '1'));
    expect(again.items.map((i) => i.index)).toEqual(['2', '3']);
  });

  it('back keeps the position (next continues after the last served item) and is idempotent', async () => {
    await startSession(3);
    await firstValueFrom(api.all(2, '1')); // #1,#2
    await firstValueFrom(api.back());
    await firstValueFrom(api.back());
    expect((await firstValueFrom(api.next())).index).toBe('3');
  });

  it('back returns 404 for an unknown session', async () => {
    holder.set('11111111-1111-1111-1111-111111111111');
    const err = await firstValueFrom(api.back()).catch((e: HttpErrorResponse) => e);
    expect((err as HttpErrorResponse).status).toBe(404);
  });
});
