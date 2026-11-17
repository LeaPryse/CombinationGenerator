import { ChangeDetectionStrategy, Component } from '@angular/core';
import { GeneratorPageComponent } from './features/generator/generator-page/generator-page.component';

@Component({
  selector: 'app-root',
  imports: [GeneratorPageComponent],
  template: `<app-generator-page />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
