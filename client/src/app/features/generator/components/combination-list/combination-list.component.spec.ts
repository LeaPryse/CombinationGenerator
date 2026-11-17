import { TestBed } from '@angular/core/testing';
import { CombinationListComponent } from './combination-list.component';

describe('CombinationListComponent', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [CombinationListComponent] }));

  it('renders one row per item with index and permutation', () => {
    const fixture = TestBed.createComponent(CombinationListComponent);
    fixture.componentRef.setInput('items', [
      { index: '3', permutation: [2, 1, 3] },
      { index: '1234', permutation: [2, 3, 1] },
    ]);
    fixture.detectChanges();
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('.item');
    expect(rows.length).toBe(2);
    expect(rows[0].querySelector('.index')!.textContent).toBe('#3');
    expect(rows[0].querySelector('.permutation')!.textContent).toBe('[2,1,3]');
    expect(rows[1].querySelector('.index')!.textContent).toBe('#1,234');
  });

  it('renders nothing for an empty page', () => {
    const fixture = TestBed.createComponent(CombinationListComponent);
    fixture.componentRef.setInput('items', []);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('.list')).toBeNull();
  });
});
