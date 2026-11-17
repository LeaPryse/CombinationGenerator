import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SinglePanelComponent } from './single-panel.component';

describe('SinglePanelComponent', () => {
  let fixture: ComponentFixture<SinglePanelComponent>;
  let el: HTMLElement;

  function text(selector: string): string {
    return (el.querySelector(selector)?.textContent ?? '').replace(/\s+/g, ' ').trim();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [SinglePanelComponent] });
    fixture = TestBed.createComponent(SinglePanelComponent);
    el = fixture.nativeElement;
    fixture.componentRef.setInput('n', 20);
    fixture.componentRef.setInput('totalCount', '2432902008176640000');
  });

  it('renders the total count, a placeholder and no message by default', () => {
    fixture.detectChanges();
    expect(text('app-total-count .value')).toBe('2,432,902,008,176,640,000');
    expect(el.querySelector('app-current-combination .placeholder')).not.toBeNull();
    expect(el.textContent).not.toContain('No more combinations');
  });

  it('renders the current combination and the no-more message', () => {
    fixture.componentRef.setInput('current', { index: '6', permutation: [3, 2, 1] });
    fixture.componentRef.setInput('noMoreMessage', 'No more combinations');
    fixture.detectChanges();
    expect(text('app-current-combination')).toBe('Combination #6 – [3,2,1]');
    expect(el.textContent).toContain('No more combinations');
  });

  it('re-emits Next, All combinations and Reset', () => {
    const events: string[] = [];
    const c = fixture.componentInstance;
    c.next.subscribe(() => events.push('next'));
    c.showAll.subscribe(() => events.push('showAll'));
    c.reset.subscribe(() => events.push('reset'));
    fixture.detectChanges();
    (el.querySelector('app-action-buttons .next') as HTMLButtonElement).click();
    (el.querySelector('app-action-buttons .show-all') as HTMLButtonElement).click();
    (el.querySelector('app-action-buttons .reset') as HTMLButtonElement).click();
    expect(events).toEqual(['next', 'showAll', 'reset']);
  });

  it('passes disabled to the action buttons', () => {
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();
    expect((el.querySelector('app-action-buttons .next') as HTMLButtonElement).disabled).toBeTrue();
  });
});
