import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PermutationItem } from '../../../../core/models/permutation.models';
import { PermutationPipe } from '../../../../shared/pipes/permutation.pipe';
import { ThousandsPipe } from '../../../../shared/pipes/thousands.pipe';

/** Shows the currently displayed permutation as "Combination #k – [..]". */
@Component({
  selector: 'app-current-combination',
  imports: [ThousandsPipe, PermutationPipe],
  template: `
    <div class="current" aria-live="polite">
      @if (item(); as current) {
        Combination #<span class="index">{{ current.index | thousands }}</span> –
        <span class="permutation">{{ current.permutation | permutation }}</span>
      } @else {
        <span class="placeholder">Press "Next" to show the first combination.</span>
      }
    </div>
  `,
  styles: `
    .current {
      font-size: 1.25rem;
      font-family: ui-monospace, Consolas, monospace;
      min-height: 1.75rem;
    }
    .placeholder {
      font-family: inherit;
      font-size: 1rem;
      color: #666;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CurrentCombinationComponent {
  readonly item = input<PermutationItem | null>(null);
}
