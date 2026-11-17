import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { PermutationItem } from '../../../../core/models/permutation.models';
import { ErrorMessageComponent } from '../../../../shared/components/error-message/error-message.component';
import { PaginatorComponent } from '../../../../shared/components/paginator/paginator.component';
import { ActionButtonsComponent } from '../action-buttons/action-buttons.component';
import { CombinationListComponent } from '../combination-list/combination-list.component';
import { PageSizeSelectComponent } from '../page-size-select/page-size-select.component';
import { TotalCountComponent } from '../total-count/total-count.component';

/** "All" mode panel: Back action, page-size selector, the current page of combinations and paging. */
@Component({
  selector: 'app-all-panel',
  imports: [
    TotalCountComponent,
    ActionButtonsComponent,
    ErrorMessageComponent,
    PageSizeSelectComponent,
    CombinationListComponent,
    PaginatorComponent,
  ],
  templateUrl: './all-panel.component.html',
  styleUrl: './all-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AllPanelComponent {
  readonly n = input.required<number>();
  /** n! as a decimal string (may exceed 2^53). */
  readonly totalCount = input.required<string>();
  readonly items = input.required<readonly PermutationItem[]>();
  /** Current page as a decimal string. */
  readonly page = input.required<string>();
  /** Total pages as a decimal string (may exceed 2^53). */
  readonly totalPages = input.required<string>();
  readonly pageSize = input.required<number>();
  readonly noMoreMessage = input<string | null>(null);
  readonly disabled = input(false);

  readonly back = output<void>();
  readonly pageSizeChange = output<number>();
  readonly pageChange = output<string>();

  /** Paging controls are hidden when there is nothing after the anchor ("No more combinations"). */
  protected readonly hasPages = computed(() => BigInt(this.totalPages()) > 0n);
}
