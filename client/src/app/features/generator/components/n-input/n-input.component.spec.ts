import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NInputComponent } from './n-input.component';

describe('NInputComponent', () => {
  let fixture: ComponentFixture<NInputComponent>;
  let el: HTMLElement;
  let emitted: number[];

  function type(value: string): void {
    const input = el.querySelector('input') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  function submitButton(): HTMLButtonElement {
    return el.querySelector('button[type=submit]') as HTMLButtonElement;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [NInputComponent] });
    fixture = TestBed.createComponent(NInputComponent);
    el = fixture.nativeElement;
    emitted = [];
    fixture.componentInstance.submitted.subscribe((n) => emitted.push(n));
    fixture.detectChanges();
  });

  it('keeps Start enabled and shows an error when submitted empty', () => {
    expect(submitButton().disabled).toBeFalse();
    submitButton().click();
    fixture.detectChanges();
    expect(emitted).toEqual([]);
    expect(el.querySelector('.validation')).not.toBeNull();
  });

  it('emits a valid n on submit', () => {
    type('3');
    submitButton().click();
    expect(emitted).toEqual([3]);
  });

  it('accepts the bounds 1 and 20', () => {
    type('1');
    submitButton().click();
    type('20');
    submitButton().click();
    expect(emitted).toEqual([1, 20]);
  });

  for (const bad of ['0', '21', '2.5', '-1']) {
    it(`rejects ${bad} with a validation message`, () => {
      type(bad);
      (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
      fixture.detectChanges();
      const input = el.querySelector('input') as HTMLInputElement;
      expect(emitted).toEqual([]);
      expect(el.querySelector('.validation')).not.toBeNull();
      expect(input.getAttribute('aria-invalid')).toBe('true');
      expect(input.getAttribute('aria-describedby')).toBe('n-error');
      expect(document.activeElement).toBe(input);
    });
  }

  it('does not emit while disabled', () => {
    fixture.componentRef.setInput('disabled', true);
    type('3');
    expect(submitButton().disabled).toBeTrue();
    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    expect(emitted).toEqual([]);
  });
});
