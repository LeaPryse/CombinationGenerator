import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { PageSizeSelectComponent } from '../components/page-size-select/page-size-select.component';
import { PERMUTATION_API } from '../../../core/api/permutation-api';
import { PermutationApiMock } from '../../../core/api/permutation-api.mock';
import { GeneratorPageComponent } from './generator-page.component';

/**
 * End-to-end UI scenario for n = 3 against the in-memory mock API (contract reference data).
 */
describe('GeneratorPageComponent (against mock API)', () => {
  let fixture: ComponentFixture<GeneratorPageComponent>;
  let el: HTMLElement;

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve));
    fixture.detectChanges();
  }

  function click(selector: string): void {
    const button = el.querySelector(selector) as HTMLButtonElement | null;
    if (!button) throw new Error(`Button not found: ${selector}`);
    button.click();
  }

  function text(selector: string): string {
    return (el.querySelector(selector)?.textContent ?? '').replace(/\s+/g, ' ').trim();
  }

  function listIndices(): string[] {
    return Array.from(el.querySelectorAll('app-combination-list .index')).map(
      (e) => e.textContent!,
    );
  }

  async function enterN(value: string): Promise<void> {
    const input = el.querySelector('app-n-input input') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    click('app-n-input button[type=submit]');
    await settle();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [GeneratorPageComponent],
      providers: [{ provide: PERMUTATION_API, useExisting: PermutationApiMock }],
    });
    TestBed.inject(PermutationApiMock).latencyMs = 0;
    fixture = TestBed.createComponent(GeneratorPageComponent);
    el = fixture.nativeElement;
    fixture.detectChanges();
  });

  it('runs the n = 3 scenario: start, next, all, paging, back, no more, reset', async () => {
    expect(el.querySelector('app-n-input')).not.toBeNull();

    await enterN('3');
    expect(text('app-total-count')).toContain('6');
    expect(el.querySelector('app-current-combination .placeholder')).not.toBeNull();

    click('app-action-buttons .next');
    await settle();
    expect(text('app-current-combination')).toBe('Combination #1 – [1,2,3]');

    click('app-action-buttons .next');
    await settle();
    expect(text('app-current-combination')).toBe('Combination #2 – [1,3,2]');

    // All mode starts right after the displayed combination (#2).
    click('app-action-buttons .show-all');
    await settle();
    expect(el.querySelector('app-action-buttons .back')).not.toBeNull();
    expect(el.querySelector('app-action-buttons .next')).toBeNull();
    expect(listIndices()).toEqual(['#3', '#4', '#5', '#6']);

    // Page size 2: anchor stays fixed -> pages #3,#4 and #5,#6.
    const pageSizeSelect = fixture.debugElement.query(By.directive(PageSizeSelectComponent));
    (pageSizeSelect.componentInstance as PageSizeSelectComponent).pageSizeChange.emit(2);
    await settle();
    expect(listIndices()).toEqual(['#3', '#4']);
    expect(text('app-paginator .summary')).toBe('Page 1 of 2');

    click('app-paginator .next');
    await settle();
    expect(listIndices()).toEqual(['#5', '#6']);
    expect(text('app-paginator .summary')).toBe('Page 2 of 2');

    // Back shows the last combination of the displayed page.
    click('app-action-buttons .back');
    await settle();
    expect(text('app-current-combination')).toBe('Combination #6 – [3,2,1]');

    click('app-action-buttons .next');
    await settle();
    expect(text('app-current-combination')).toBe('Combination #6 – [3,2,1]');
    expect(el.textContent).toContain('No more combinations');

    click('app-action-buttons .reset');
    await settle();
    expect(el.querySelector('app-n-input')).not.toBeNull();
    expect(el.querySelector('app-total-count')).toBeNull();
  });

  it('Back, then All again starts right after the shown combination (#6 after Back on #5)', async () => {
    await enterN('3');
    click('app-action-buttons .next');
    await settle();
    expect(text('app-current-combination')).toBe('Combination #1 – [1,2,3]');

    // Set page size 2 before entering All mode (single mode only stores the size).
    click('app-action-buttons .show-all');
    await settle();
    const pageSizeSelect = fixture.debugElement.query(By.directive(PageSizeSelectComponent));
    (pageSizeSelect.componentInstance as PageSizeSelectComponent).pageSizeChange.emit(2);
    await settle();
    expect(listIndices()).toEqual(['#2', '#3']);

    click('app-paginator .next');
    await settle();
    expect(listIndices()).toEqual(['#4', '#5']);

    click('app-action-buttons .back');
    await settle();
    expect(text('app-current-combination')).toBe('Combination #5 – [3,1,2]');

    click('app-action-buttons .show-all');
    await settle();
    expect(listIndices()).toEqual(['#6']);
    expect(text('app-paginator .summary')).toBe('Page 1 of 1');
  });

  it('shows "No more combinations" in All mode after the last permutation', async () => {
    await enterN('1');
    click('app-action-buttons .next');
    await settle();
    click('app-action-buttons .show-all');
    await settle();
    expect(el.textContent).toContain('No more combinations');
    expect(el.querySelector('app-paginator')).toBeNull();
    click('app-action-buttons .back');
    await settle();
    expect(text('app-current-combination')).toBe('Combination #1 – [1]');
  });

  it('formats 20! with thousands separators', async () => {
    await enterN('20');
    expect(text('app-total-count .value')).toBe('2,432,902,008,176,640,000');
  });
});
