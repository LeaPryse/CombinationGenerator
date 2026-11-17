import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ErrorMessageComponent } from './error-message.component';

describe('ErrorMessageComponent', () => {
  let fixture: ComponentFixture<ErrorMessageComponent>;
  let el: HTMLElement;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ErrorMessageComponent] });
    fixture = TestBed.createComponent(ErrorMessageComponent);
    el = fixture.nativeElement;
  });

  it('renders nothing without a message', () => {
    fixture.detectChanges();
    expect(el.querySelector('.banner')).toBeNull();
  });

  it('renders an error as an alert', () => {
    fixture.componentRef.setInput('message', 'Boom');
    fixture.detectChanges();
    const banner = el.querySelector('.banner')!;
    expect(banner.textContent).toContain('Boom');
    expect(banner.getAttribute('role')).toBe('alert');
  });

  it('renders the info variant as a status', () => {
    fixture.componentRef.setInput('message', 'No more combinations');
    fixture.componentRef.setInput('variant', 'info');
    fixture.componentRef.setInput('dismissible', false);
    fixture.detectChanges();
    const banner = el.querySelector('.banner')!;
    expect(banner.classList).toContain('info');
    expect(banner.getAttribute('role')).toBe('status');
    expect(el.querySelector('.dismiss')).toBeNull();
  });

  it('emits dismissed when the close button is clicked', () => {
    let count = 0;
    fixture.componentInstance.dismissed.subscribe(() => count++);
    fixture.componentRef.setInput('message', 'Boom');
    fixture.detectChanges();
    (el.querySelector('.dismiss') as HTMLButtonElement).click();
    expect(count).toBe(1);
  });
});
