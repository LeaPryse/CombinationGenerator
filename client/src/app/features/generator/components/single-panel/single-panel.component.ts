import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { PermutationItem } from '../../../../core/models/permutation.models';
import { ErrorMessageComponent } from '../../../../shared/components/error-message/error-message.component';
import { ActionButtonsComponent } from '../action-buttons/action-buttons.component';
import { CurrentCombinationComponent } from '../current-combination/current-combination.component';
import { TotalCountComponent } from '../total-count/total-count.component';

/** "Single" mode panel: total count, current combination and the Next / All / Reset actions. */
@Component({
  selector: 'app-single-panel',
  imports: [
    TotalCountComponent,
    CurrentCombinationComponent,
    ErrorMessageComponent,
    ActionButtonsComponent,
  ],
  templateUrl: './single-panel.component.html',
  styleUrl: './single-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SinglePanelComponent {
  readonly n = input.required<number>();
  /** n! as a decimal string (may exceed 2^53). */
  readonly totalCount = input.required<string>();
  readonly current = input<PermutationItem | null>(null);
  readonly noMoreMessage = input<string | null>(null);
  readonly disabled = input(false);

  readonly next = output<void>();
  readonly showAll = output<void>();
  readonly reset = output<void>();
}
