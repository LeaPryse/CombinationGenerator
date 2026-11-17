import { Pipe, PipeTransform } from '@angular/core';

/** Formats a permutation as `[1,2,3]`. */
@Pipe({ name: 'permutation' })
export class PermutationPipe implements PipeTransform {
  transform(value: readonly number[] | null | undefined): string {
    return value ? `[${value.join(',')}]` : '';
  }
}
