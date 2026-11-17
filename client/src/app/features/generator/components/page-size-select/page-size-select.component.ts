import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

export const PAGE_SIZE_OPTIONS: readonly number[] = [5, 10, 25, 50, 100];

/** Lets the user choose how many permutations are shown per page. */
@Component({
  selector: 'app-page-size-select',
  template: `
    <label class="page-size">
      Per page
      <select [disabled]="disabled()" (change)="onChange($event)">
        @for (option of options(); track option) {
          <option [value]="option" [selected]="option === pageSize()">{{ option }}</option>
        }
      </select>
    </label>
  `,
  styles: `
    .page-size {
      display: inline-flex;
      gap: 0.5rem;
      align-items: center;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageSizeSelectComponent {
  readonly pageSize = input.required<number>();
  readonly options = input<readonly number[]>(PAGE_SIZE_OPTIONS);
  readonly disabled = input(false);
  readonly pageSizeChange = output<number>();

  protected onChange(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);
    if (Number.isInteger(value) && value !== this.pageSize()) {
      this.pageSizeChange.emit(value);
    }
  }
}
