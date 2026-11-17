import { Injectable, signal } from '@angular/core';

/**
 * Holds the current server session id.
 *
 * Kept separate from the store so the HTTP interceptor can read it without creating a
 * circular dependency (store -> api -> HttpClient -> interceptor -> store).
 */
@Injectable({ providedIn: 'root' })
export class SessionIdHolder {
  private readonly _sessionId = signal<string | null>(null);
  readonly sessionId = this._sessionId.asReadonly();

  set(sessionId: string | null): void {
    this._sessionId.set(sessionId);
  }

  clear(): void {
    this._sessionId.set(null);
  }
}
