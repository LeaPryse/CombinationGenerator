import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActionButtonsComponent } from './action-buttons.component';

describe('ActionButtonsComponent', () => {
  let fixture: ComponentFixture<ActionButtonsComponent>;
  let el: HTMLElement;
  let events: string[];

  function btn(selector: string): HTMLButtonElement | null {
    return el.querySelector(selector);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ActionButtonsComponent] });
    fixture = TestBed.createComponent(ActionButtonsComponent);
    el = fixture.nativeElement;
    events = [];
    const c = fixture.componentInstance;
    c.next.subscribe(() => events.push('next'));
    c.showAll.subscribe(() => events.push('showAll'));
    c.reset.subscribe(() => events.push('reset'));
    c.back.subscribe(() => events.push('back'));
  });

  it('single mode shows Next, All combinations and Reset, and emits them', () => {
    fixture.componentRef.setInput('mode', 'single');
    fixture.detectChanges();
    expect(btn('.back')).toBeNull();
    btn('.next')!.click();
    btn('.show-all')!.click();
    btn('.reset')!.click();
    expect(events).toEqual(['next', 'showAll', 'reset']);
  });

  it('all mode shows only Back', () => {
    fixture.componentRef.setInput('mode', 'all');
    fixture.detectChanges();
    expect(btn('.next')).toBeNull();
    expect(btn('.reset')).toBeNull();
    btn('.back')!.click();
    expect(events).toEqual(['back']);
  });

  it('disabled disables request buttons but keeps Reset enabled', () => {
    fixture.componentRef.setInput('mode', 'single');
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();
    expect(btn('.next')!.disabled).toBeTrue();
    expect(btn('.show-all')!.disabled).toBeTrue();
    expect(btn('.reset')!.disabled).toBeFalse();
  });
});
