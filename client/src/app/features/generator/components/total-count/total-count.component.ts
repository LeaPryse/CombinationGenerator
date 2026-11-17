import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ThousandsPipe } from '../../../../shared/pipes/thousands.pipe';

/** Displays n! formatted with thousands separators (full precision, value is a string). */
@Component({
  selector: 'app-total-count',
  imports: [ThousandsPipe],
  template: `
    <p class="total-count">
      Total number of ways to arrange {{ n() }} numbers in a row:
      <strong class="value">{{ totalCount() | thousands }}</strong>
    </p>
  `,
  styles: `
    .total-count {
      margin: 0;
      font-size: 1.1rem;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TotalCountComponent {
  readonly n = input.required<number>();
  readonly totalCount = input.required<string>();
}
