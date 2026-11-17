import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { PERMUTATION_API } from './permutation-api';
import { PermutationApiService } from './permutation-api.service';

describe('PermutationApiService', () => {
  let service: PermutationApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(PermutationApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('is the default PERMUTATION_API implementation', () => {
    expect(TestBed.inject(PERMUTATION_API)).toBe(service);
  });

  it('start POSTs { n } to /api/permutations/start', async () => {
    const result = firstValueFrom(service.start(3));
    const req = http.expectOne('/api/permutations/start');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ n: 3 });
    req.flush({ sessionId: 'abc', n: 3, totalCount: '6' });
    expect(await result).toEqual({ sessionId: 'abc', n: 3, totalCount: '6' });
  });

  it('next GETs /api/permutations/next', async () => {
    const result = firstValueFrom(service.next());
    const req = http.expectOne('/api/permutations/next');
    expect(req.request.method).toBe('GET');
    req.flush({ index: '1', permutation: [1, 2, 3], hasMore: true, message: null });
    expect((await result).index).toBe('1');
  });

  it('back POSTs to /api/permutations/back without a body and completes on 204', async () => {
    const result = firstValueFrom(service.back());
    const req = http.expectOne('/api/permutations/back');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toBeNull();
    req.flush(null, { status: 204, statusText: 'No Content' });
    await expectAsync(result).toBeResolved();
  });

  it('back surfaces a 404 as an HttpErrorResponse', async () => {
    const result = firstValueFrom(service.back());
    http
      .expectOne('/api/permutations/back')
      .flush({ title: 'Session not found', status: 404 }, { status: 404, statusText: 'Not Found' });
    await expectAsync(result).toBeRejectedWith(jasmine.objectContaining({ status: 404 }));
  });

  it('all sends pageSize and page as query params, page beyond 2^53 as exact text', () => {
    service.all(1, 2432902008176640000n).subscribe();
    const req = http.expectOne(
      (r) => r.url === '/api/permutations/all' && r.method === 'GET',
    );
    expect(req.request.params.get('pageSize')).toBe('1');
    expect(req.request.params.get('page')).toBe('2432902008176640000');
    req.flush({
      items: [],
      page: '1',
      pageSize: 1,
      totalPages: '0',
      totalItems: '0',
      firstIndex: null,
      message: 'No more combinations',
    });
  });
});
