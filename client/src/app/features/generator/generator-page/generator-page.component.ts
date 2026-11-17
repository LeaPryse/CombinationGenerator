import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ErrorMessageComponent } from '../../../shared/components/error-message/error-message.component';
import { PermutationStore } from '../../../state/permutation.store';
import { AllPanelComponent } from '../components/all-panel/all-panel.component';
import { NInputComponent } from '../components/n-input/n-input.component';
import { SinglePanelComponent } from '../components/single-panel/single-panel.component';

/** Smart container: the only component that talks to the store. */
@Component({
  selector: 'app-generator-page',
  imports: [NInputComponent, SinglePanelComponent, AllPanelComponent, ErrorMessageComponent],
  templateUrl: './generator-page.component.html',
  styleUrl: './generator-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GeneratorPageComponent {
  protected readonly store = inject(PermutationStore);
}
