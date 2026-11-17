/**
 * DTOs mirroring docs/API_CONTRACT.md (v1.0).
 *
 * Large numbers (counts, indices, pages) are transported as decimal strings because they can
 * exceed Number.MAX_SAFE_INTEGER (20! ≈ 2.4·10^18). Use BigInt for arithmetic, never Number().
 */

/** A decimal integer encoded as a JSON string (may exceed 2^53). */
export type BigIntString = string;

export interface StartRequest {
  n: number;
}

export interface StartResponse {
  sessionId: string;
  n: number;
  totalCount: BigIntString;
}

export interface NextResponse {
  index: BigIntString | null;
  permutation: number[] | null;
  hasMore: boolean;
  message: string | null;
}

export interface PermutationItem {
  index: BigIntString;
  permutation: number[];
}

export interface PageResponse {
  items: PermutationItem[];
  page: BigIntString;
  pageSize: number;
  totalPages: BigIntString;
  totalItems: BigIntString;
  firstIndex: BigIntString | null;
  message: string | null;
}

/** RFC 7807 ProblemDetails as returned by the API for every error. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  traceId?: string;
}

export const N_MIN = 1;
export const N_MAX = 20;
export const PAGE_SIZE_MIN = 1;
export const PAGE_SIZE_MAX = 1000;
export const DEFAULT_PAGE_SIZE = 10;
