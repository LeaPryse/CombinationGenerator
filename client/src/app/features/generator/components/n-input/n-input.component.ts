import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { N_MAX, N_MIN } from '../../../../core/models/permutation.models';

/** Asks the user for n (integer 1..20) and emits it on submit. */
@Component({
  selector: 'app-n-input',
  imports: [ReactiveFormsModule],
  template: `
    <form class="n-input" (submit)="$event.preventDefault(); submit(nInput)">
      <label for="n-value">Enter n ({{ min }}–{{ max }}):</label>
      <input
        #nInput
        id="n-value"
        type="number"
        [min]="min"
        [max]="max"
        step="1"
        [formControl]="control"
        [attr.aria-invalid]="showError()"
        [attr.aria-describedby]="showError() ? 'n-error' : null"
      />
      <button type="submit" [disabled]="disabled()">Start</button>
      @if (showError()) {
        <p id="n-error" class="validation" role="alert">
          n must be a whole number between {{ min }} and {{ max }}.
        </p>
      }
    </form>
  `,
  styles: `
    .n-input {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
    }
    input {
      width: 6rem;
    }
    .validation {
      flex-basis: 100%;
      margin: 0;
      color: #b91c1c;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NInputComponent {
  readonly disabled = input(false);
  readonly submitted = output<number>();

  protected readonly min = N_MIN;
  protected readonly max = N_MAX;

  protected readonly control = new FormControl<number | null>(null, [
    Validators.required,
    Validators.min(N_MIN),
    Validators.max(N_MAX),
    Validators.pattern(/^\d+$/),
  ]);

  protected showError(): boolean {
    return this.control.invalid && (this.control.dirty || this.control.touched);
  }

  /** On a failed submit the error is shown and focus moves to the invalid field. */
  protected submit(input?: HTMLInputElement): void {
    this.control.markAsTouched();
    const value = this.control.value;
    if (this.disabled()) return;
    if (this.control.invalid || value === null) {
      input?.focus();
      return;
    }
    this.submitted.emit(Number(value));
  }
}
