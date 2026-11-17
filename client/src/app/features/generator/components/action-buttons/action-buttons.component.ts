import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/**
 * Single mode: Next, All combinations, Reset. All mode: Back.
 */
@Component({
  selector: 'app-action-buttons',
  template: `
    <div class="actions">
      @if (mode() === 'all') {
        <button type="button" class="back" [disabled]="disabled()" (click)="back.emit()">
          Back
        </button>
      } @else {
        <button type="button" class="next" [disabled]="disabled()" (click)="next.emit()">
          Next
        </button>
        <button type="button" class="show-all" [disabled]="disabled()" (click)="showAll.emit()">
          All combinations
        </button>
        <button type="button" class="reset" (click)="reset.emit()">Reset</button>
      }
    </div>
  `,
  styles: `
    .actions {
      display: flex;
      gap: 0.5rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActionButtonsComponent {
  readonly mode = input.required<'single' | 'all'>();
  /** Disables the request-triggering buttons (Reset stays enabled to abandon a slow request). */
  readonly disabled = input(false);

  readonly next = output<void>();
  readonly showAll = output<void>();
  readonly back = output<void>();
  readonly reset = output<void>();
}
