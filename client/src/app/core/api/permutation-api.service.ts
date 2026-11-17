import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  NextResponse,
  PageResponse,
  StartRequest,
  StartResponse,
} from '../models/permutation.models';
import type { PermutationApi } from './permutation-api';

/** Base path of the permutations API (proxied to the server in development). */
export const PERMUTATIONS_API_BASE = '/api/permutations';

/** Typed, stateless wrapper over HttpClient. The X-Session-Id header is added by the interceptor. */
@Injectable({ providedIn: 'root' })
export class PermutationApiService implements PermutationApi {
  private readonly http = inject(HttpClient);

  start(n: number): Observable<StartResponse> {
    const body: StartRequest = { n };
    return this.http.post<StartResponse>(`${PERMUTATIONS_API_BASE}/start`, body);
  }

  next(): Observable<NextResponse> {
    return this.http.get<NextResponse>(`${PERMUTATIONS_API_BASE}/next`);
  }

  all(pageSize: number, page: string | bigint): Observable<PageResponse> {
    const params = new HttpParams().set('pageSize', pageSize).set('page', page.toString());
    return this.http.get<PageResponse>(`${PERMUTATIONS_API_BASE}/all`, { params });
  }

  /** `POST /back` with no body; the server answers `204 No Content`. */
  back(): Observable<void> {
    return this.http.post<void>(`${PERMUTATIONS_API_BASE}/back`, null);
  }
}
