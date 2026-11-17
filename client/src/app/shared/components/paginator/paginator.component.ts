import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { ThousandsPipe } from '../../pipes/thousands.pipe';

/** Maximum number of page-number buttons shown at once. */
export const PAGE_WINDOW = 7;

/**
 * Presentational paginator: First, Prev, windowed page numbers, jump-to-page, Next, Last.
 * Page numbers are decimal strings and all arithmetic uses BigInt, so page counts beyond 2^53
 * (n = 20 with a small page size) work correctly.
 */
@Component({
  selector: 'app-paginator',
  imports: [ThousandsPipe],
  templateUrl: './paginator.component.html',
  styleUrl: './paginator.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaginatorComponent {
  /** Current page (1-based, decimal string). */
  readonly page = input.required<string>();
  /** Total number of pages (decimal string). */
  readonly totalPages = input.required<string>();
  readonly disabled = input(false);

  /** Emits the requested page as a decimal string. */
  readonly pageChange = output<string>();

  protected readonly jumpText = signal('');

  private readonly pageBig = computed(() => BigInt(this.page()));
  private readonly totalBig = computed(() => BigInt(this.totalPages()));

  protected readonly isFirst = computed(() => this.pageBig() <= 1n);
  protected readonly isLast = computed(() => this.pageBig() >= this.totalBig());

  /** Window of at most PAGE_WINDOW page numbers centred on the current page. */
  protected readonly windowPages = computed<string[]>(() => {
    const total = this.totalBig();
    if (total < 1n) return [];
    const size = BigInt(PAGE_WINDOW);
    const half = size / 2n;
    let start = this.pageBig() - half;
    if (start < 1n) start = 1n;
    let end = start + size - 1n;
    if (end > total) {
      end = total;
      start = end - size + 1n;
      if (start < 1n) start = 1n;
    }
    const pages: string[] = [];
    for (let p = start; p <= end; p++) pages.push(p.toString());
    return pages;
  });

  /** Parsed jump target, or null when the text is not a valid page number. */
  protected readonly jumpTarget = computed<string | null>(() => {
    const text = this.jumpText().trim().replace(/,/g, '');
    if (!/^\d+$/.test(text)) return null;
    const target = BigInt(text);
    return target >= 1n && target <= this.totalBig() ? target.toString() : null;
  });

  protected readonly jumpInvalid = computed(
    () => this.jumpText().trim() !== '' && this.jumpTarget() === null,
  );

  protected first(): void {
    this.go(1n);
  }

  protected prev(): void {
    this.go(this.pageBig() - 1n);
  }

  protected next(): void {
    this.go(this.pageBig() + 1n);
  }

  protected last(): void {
    this.go(this.totalBig());
  }

  protected select(page: string): void {
    this.go(BigInt(page));
  }

  protected onJumpInput(event: Event): void {
    this.jumpText.set((event.target as HTMLInputElement).value);
  }

  protected jump(): void {
    const target = this.jumpTarget();
    if (target === null) return;
    this.go(BigInt(target));
    this.jumpText.set('');
  }

  private go(target: bigint): void {
    if (this.disabled()) return;
    if (target < 1n || target > this.totalBig() || target === this.pageBig()) return;
    this.pageChange.emit(target.toString());
  }
}
