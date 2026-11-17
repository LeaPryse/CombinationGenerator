import { TestBed } from '@angular/core/testing';
import { CurrentCombinationComponent } from './current-combination.component';

describe('CurrentCombinationComponent', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [CurrentCombinationComponent] }));

  it('shows a placeholder when nothing is displayed yet', () => {
    const fixture = TestBed.createComponent(CurrentCombinationComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.placeholder')).not.toBeNull();
  });

  it('shows the index and permutation', () => {
    const fixture = TestBed.createComponent(CurrentCombinationComponent);
    fixture.componentRef.setInput('item', { index: '2', permutation: [1, 3, 2] });
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent!.replace(/\s+/g, ' ');
    expect(text).toContain('Combination #2 – [1,3,2]');
  });

  it('formats large indices with separators', () => {
    const fixture = TestBed.createComponent(CurrentCombinationComponent);
    fixture.componentRef.setInput('item', { index: '2432902008176640000', permutation: [2, 1] });
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.index')!.textContent).toBe('2,432,902,008,176,640,000');
  });
});
