import { EnvironmentProviders, Provider } from '@angular/core';
import { PERMUTATION_API } from '../app/core/api/permutation-api';
import { PermutationApiMock } from '../app/core/api/permutation-api.mock';

/** Mock environment (`ng serve --configuration mock`): the UI runs against the in-memory mock API. */
export const environment: { providers: (Provider | EnvironmentProviders)[] } = {
  providers: [{ provide: PERMUTATION_API, useExisting: PermutationApiMock }],
};
