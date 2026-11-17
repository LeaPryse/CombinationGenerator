import { InjectionToken, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { NextResponse, PageResponse, StartResponse } from '../models/permutation.models';
import { PermutationApiService } from './permutation-api.service';

/** Contract shared by the HTTP implementation and the in-memory mock. */
export interface PermutationApi {
  start(n: number): Observable<StartResponse>;
  next(): Observable<NextResponse>;
  /** `page` is a decimal string or BigInt so pages beyond 2^53 are addressable. */
  all(pageSize: number, page: string | bigint): Observable<PageResponse>;
  /**
   * Leaves "All" mode on the server (clears the paging anchor, keeps the position). Emits once without
   * a payload (`204 No Content`) on success, so the next `all` starts right after the shown item.
   */
  back(): Observable<void>;
}

/**
 * Injection token used by the store. Defaults to the real HTTP implementation; the `mock` build
 * configuration swaps it for {@link PermutationApiMock} via `environment.mock.ts`.
 */
export const PERMUTATION_API = new InjectionToken<PermutationApi>('PERMUTATION_API', {
  providedIn: 'root',
  factory: () => inject(PermutationApiService),
});
