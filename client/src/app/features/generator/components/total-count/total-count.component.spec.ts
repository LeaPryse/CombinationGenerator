import { TestBed } from '@angular/core/testing';
import { TotalCountComponent } from './total-count.component';

describe('TotalCountComponent', () => {
  it('renders n and the formatted total count with full precision', () => {
    TestBed.configureTestingModule({ imports: [TotalCountComponent] });
    const fixture = TestBed.createComponent(TotalCountComponent);
    fixture.componentRef.setInput('n', 20);
    fixture.componentRef.setInput('totalCount', '2432902008176640000');
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain('20 numbers');
    expect(text).toContain('2,432,902,008,176,640,000');
  });
});
