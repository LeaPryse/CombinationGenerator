import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { PaginatorComponent } from '../../../../shared/components/paginator/paginator.component';
import { PageSizeSelectComponent } from '../page-size-select/page-size-select.component';
import { AllPanelComponent } from './all-panel.component';

describe('AllPanelComponent', () => {
  let fixture: ComponentFixture<AllPanelComponent>;
  let el: HTMLElement;

  function text(selector: string): string {
    return (el.querySelector(selector)?.textContent ?? '').replace(/\s+/g, ' ').trim();
  }

  function setPage(totalPages: string, items = [{ index: '3', permutation: [2, 1, 3] }]): void {
    fixture.componentRef.setInput('items', items);
    fixture.componentRef.setInput('page', '1');
    fixture.componentRef.setInput('totalPages', totalPages);
    fixture.componentRef.setInput('pageSize', 2);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [AllPanelComponent] });
    fixture = TestBed.createComponent(AllPanelComponent);
    el = fixture.nativeElement;
    fixture.componentRef.setInput('n', 3);
    fixture.componentRef.setInput('totalCount', '6');
  });

  it('renders the list, page-size select and paginator when there are pages', () => {
    setPage('2', [
      { index: '3', permutation: [2, 1, 3] },
      { index: '4', permutation: [2, 3, 1] },
    ]);
    fixture.detectChanges();
    expect(text('app-total-count .value')).toBe('6');
    expect(el.querySelectorAll('app-combination-list .index').length).toBe(2);
    expect(el.querySelector('app-page-size-select')).not.toBeNull();
    expect(text('app-paginator .summary')).toBe('Page 1 of 2');
    expect(el.querySelector('app-action-buttons .back')).not.toBeNull();
  });

  it('hides paging controls and shows the message when there are no pages', () => {
    setPage('0', []);
    fixture.componentRef.setInput('noMoreMessage', 'No more combinations');
    fixture.detectChanges();
    expect(el.querySelector('app-combination-list')).toBeNull();
    expect(el.querySelector('app-paginator')).toBeNull();
    expect(el.querySelector('app-page-size-select')).toBeNull();
    expect(el.textContent).toContain('No more combinations');
  });

  it('handles totalPages beyond 2^53', () => {
    setPage('2432902008176640000');
    fixture.detectChanges();
    expect(el.querySelector('app-paginator')).not.toBeNull();
  });

  it('re-emits back, pageSizeChange and pageChange', () => {
    setPage('3');
    const c = fixture.componentInstance;
    const events: unknown[] = [];
    c.back.subscribe(() => events.push('back'));
    c.pageSizeChange.subscribe((size) => events.push(size));
    c.pageChange.subscribe((p) => events.push(p));
    fixture.detectChanges();

    (el.querySelector('app-action-buttons .back') as HTMLButtonElement).click();
    fixture.debugElement
      .query(By.directive(PageSizeSelectComponent))
      .componentInstance.pageSizeChange.emit(5);
    fixture.debugElement.query(By.directive(PaginatorComponent)).componentInstance.pageChange.emit('2');
    expect(events).toEqual(['back', 5, '2']);
  });
});
