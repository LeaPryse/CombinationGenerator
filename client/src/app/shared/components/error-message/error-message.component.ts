import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** Small banner for errors (role=alert) or informational messages (role=status). */
@Component({
  selector: 'app-error-message',
  template: `
    @if (message(); as text) {
      <div
        class="banner"
        [class.info]="variant() === 'info'"
        [attr.role]="variant() === 'error' ? 'alert' : 'status'"
      >
        <span class="text">{{ text }}</span>
        @if (dismissible()) {
          <button type="button" class="dismiss" aria-label="Dismiss" (click)="dismissed.emit()">
            ×
          </button>
        }
      </div>
    }
  `,
  styles: `
    .banner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      padding: 0.5rem 0.75rem;
      border: 1px solid #fca5a5;
      border-radius: 4px;
      background: #fef2f2;
      color: #991b1b;
    }
    .banner.info {
      border-color: #93c5fd;
      background: #eff6ff;
      color: #1e3a8a;
    }
    .dismiss {
      border: none;
      background: transparent;
      font-size: 1.1rem;
      cursor: pointer;
      color: inherit;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ErrorMessageComponent {
  readonly message = input<string | null>(null);
  readonly variant = input<'error' | 'info'>('error');
  readonly dismissible = input(true);
  readonly dismissed = output<void>();
}
