import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { PERMUTATIONS_API_BASE } from '../api/permutation-api.service';
import { SessionIdHolder } from '../session/session-id.holder';

export const SESSION_HEADER = 'X-Session-Id';

/**
 * Adds `X-Session-Id` to every permutations API call (`next`, `all`, `back`) except `start`
 * (which creates the session).
 */
export const sessionInterceptor: HttpInterceptorFn = (req, next) => {
  const sessionId = inject(SessionIdHolder).sessionId();
  const isApiCall = req.url.startsWith(PERMUTATIONS_API_BASE);
  const isStart = req.url === `${PERMUTATIONS_API_BASE}/start`;

  if (!sessionId || !isApiCall || isStart) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { [SESSION_HEADER]: sessionId } }));
};
