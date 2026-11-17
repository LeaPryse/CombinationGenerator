import { TestBed } from '@angular/core/testing';
import { SessionIdHolder } from './session-id.holder';

describe('SessionIdHolder', () => {
  it('stores and clears the session id', () => {
    const holder = TestBed.inject(SessionIdHolder);
    expect(holder.sessionId()).toBeNull();
    holder.set('abc');
    expect(holder.sessionId()).toBe('abc');
    holder.clear();
    expect(holder.sessionId()).toBeNull();
  });
});
