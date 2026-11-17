import { TestBed } from '@angular/core/testing';
import { PageSizeSelectComponent } from './page-size-select.component';

describe('PageSizeSelectComponent', () => {
  it('selects the current size and emits a changed size', () => {
    TestBed.configureTestingModule({ imports: [PageSizeSelectComponent] });
    const fixture = TestBed.createComponent(PageSizeSelectComponent);
    fixture.componentRef.setInput('pageSize', 10);
    fixture.detectChanges();
    const emitted: number[] = [];
    fixture.componentInstance.pageSizeChange.subscribe((s) => emitted.push(s));

    const select = (fixture.nativeElement as HTMLElement).querySelector('select')!;
    expect(select.value).toBe('10');

    select.value = '25';
    select.dispatchEvent(new Event('change'));
    expect(emitted).toEqual([25]);
  });
});
