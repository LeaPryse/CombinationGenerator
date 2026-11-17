import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionIdHolder } from '../session/session-id.holder';
import { SESSION_HEADER, sessionInterceptor } from './session.interceptor';

describe('sessionInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let holder: SessionIdHolder;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([sessionInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
    holder = TestBed.inject(SessionIdHolder);
  });

  afterEach(() => controller.verify());

  it('adds X-Session-Id to API calls when a session exists', () => {
    holder.set('sid-1');
    http.get('/api/permutations/next').subscribe();
    const req = controller.expectOne('/api/permutations/next');
    expect(req.request.headers.get(SESSION_HEADER)).toBe('sid-1');
    req.flush({});
  });

  it('adds X-Session-Id to back', () => {
    holder.set('sid-1');
    http.post('/api/permutations/back', null).subscribe();
    const req = controller.expectOne('/api/permutations/back');
    expect(req.request.headers.get(SESSION_HEADER)).toBe('sid-1');
    req.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('does not add the header to start', () => {
    holder.set('sid-1');
    http.post('/api/permutations/start', { n: 3 }).subscribe();
    const req = controller.expectOne('/api/permutations/start');
    expect(req.request.headers.has(SESSION_HEADER)).toBeFalse();
    req.flush({});
  });

  it('does not add the header when there is no session', () => {
    http.get('/api/permutations/next').subscribe();
    const req = controller.expectOne('/api/permutations/next');
    expect(req.request.headers.has(SESSION_HEADER)).toBeFalse();
    req.flush({});
  });

  it('does not add the header to non-API URLs', () => {
    holder.set('sid-1');
    http.get('/assets/data.json').subscribe();
    const req = controller.expectOne('/assets/data.json');
    expect(req.request.headers.has(SESSION_HEADER)).toBeFalse();
    req.flush({});
  });
});
