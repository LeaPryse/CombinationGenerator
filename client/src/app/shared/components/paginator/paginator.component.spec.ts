import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PaginatorComponent } from './paginator.component';

describe('PaginatorComponent', () => {
  let fixture: ComponentFixture<PaginatorComponent>;
  let el: HTMLElement;
  let emitted: string[];

  function setup(page: string, totalPages: string, disabled = false): void {
    fixture.componentRef.setInput('page', page);
    fixture.componentRef.setInput('totalPages', totalPages);
    fixture.componentRef.setInput('disabled', disabled);
    fixture.detectChanges();
  }

  function button(selector: string): HTMLButtonElement {
    return el.querySelector(selector) as HTMLButtonElement;
  }

  function pageNumbers(): string[] {
    return Array.from(el.querySelectorAll('.page-number')).map((b) => b.textContent!.trim());
  }

  function typeJump(value: string): void {
    const input = el.querySelector('.jump-input') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [PaginatorComponent] });
    fixture = TestBed.createComponent(PaginatorComponent);
    el = fixture.nativeElement;
    emitted = [];
    fixture.componentInstance.pageChange.subscribe((p) => emitted.push(p));
  });

  it('shows all pages when there are at most 7', () => {
    setup('1', '3');
    expect(pageNumbers()).toEqual(['1', '2', '3']);
    expect(el.querySelector('.page-number.active')!.textContent!.trim()).toBe('1');
  });

  it('shows a window of 7 pages centred on the current page', () => {
    setup('10', '20');
    expect(pageNumbers()).toEqual(['7', '8', '9', '10', '11', '12', '13']);
  });

  it('clamps the window at the start and end', () => {
    setup('2', '20');
    expect(pageNumbers()).toEqual(['1', '2', '3', '4', '5', '6', '7']);
    setup('19', '20');
    expect(pageNumbers()).toEqual(['14', '15', '16', '17', '18', '19', '20']);
  });

  it('disables First/Prev on the first page and Next/Last on the last page', () => {
    setup('1', '5');
    expect(button('.first').disabled).toBeTrue();
    expect(button('.prev').disabled).toBeTrue();
    expect(button('.next').disabled).toBeFalse();
    setup('5', '5');
    expect(button('.next').disabled).toBeTrue();
    expect(button('.last').disabled).toBeTrue();
    expect(button('.first').disabled).toBeFalse();
  });

  it('emits the target page for First, Prev, Next, Last and page numbers', () => {
    setup('5', '20');
    button('.first').click();
    button('.prev').click();
    button('.next').click();
    button('.last').click();
    (el.querySelectorAll('.page-number')[0] as HTMLButtonElement).click();
    expect(emitted).toEqual(['1', '4', '6', '20', '2']);
  });

  it('does not emit when clicking the current page', () => {
    setup('3', '5');
    (el.querySelector('.page-number.active') as HTMLButtonElement).click();
    expect(emitted).toEqual([]);
  });

  it('works with page counts beyond 2^53', () => {
    const total = '2432902008176640000';
    setup('2432902008176639999', total);
    expect(pageNumbers()).toEqual([
      '2,432,902,008,176,639,994',
      '2,432,902,008,176,639,995',
      '2,432,902,008,176,639,996',
      '2,432,902,008,176,639,997',
      '2,432,902,008,176,639,998',
      '2,432,902,008,176,639,999',
      '2,432,902,008,176,640,000',
    ]);
    button('.next').click();
    button('.prev').click();
    button('.last').click();
    expect(emitted).toEqual([total, '2432902008176639998', total]);
  });

  it('jumps to a valid page typed into the input', () => {
    setup('1', '2432902008176640000');
    typeJump('1,234,567,890,123,456,789');
    expect(button('.jump-button').disabled).toBeFalse();
    button('.jump-button').click();
    expect(emitted).toEqual(['1234567890123456789']);
  });

  it('rejects invalid jump targets', () => {
    setup('1', '10');
    typeJump('11');
    expect(button('.jump-button').disabled).toBeTrue();
    expect(el.querySelector('.jump-input.invalid')).not.toBeNull();
    typeJump('abc');
    expect(button('.jump-button').disabled).toBeTrue();
    typeJump('0');
    expect(button('.jump-button').disabled).toBeTrue();
  });

  it('disables all buttons when disabled', () => {
    setup('5', '10', true);
    const buttons = Array.from(el.querySelectorAll('button')) as HTMLButtonElement[];
    expect(buttons.every((b) => b.disabled)).toBeTrue();
  });
});
