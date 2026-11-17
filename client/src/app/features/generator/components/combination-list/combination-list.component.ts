import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { PermutationItem } from '../../../../core/models/permutation.models';
import { PermutationPipe } from '../../../../shared/pipes/permutation.pipe';
import { ThousandsPipe } from '../../../../shared/pipes/thousands.pipe';

/** Renders one page of permutations. */
@Component({
  selector: 'app-combination-list',
  imports: [ThousandsPipe, PermutationPipe],
  template: `
    @if (items().length > 0) {
      <ol class="list">
        @for (item of items(); track item.index) {
          <li class="item">
            <span class="index">#{{ item.index | thousands }}</span>
            <span class="permutation">{{ item.permutation | permutation }}</span>
          </li>
        }
      </ol>
    }
  `,
  styles: `
    .list {
      list-style: none;
      margin: 0;
      padding: 0;
      font-family: ui-monospace, Consolas, monospace;
    }
    .item {
      display: flex;
      gap: 1rem;
      padding: 0.2rem 0.25rem;
      border-bottom: 1px solid #eee;
    }
    .index {
      min-width: 14rem;
      text-align: right;
      color: #555;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CombinationListComponent {
  readonly items = input.required<readonly PermutationItem[]>();
}
