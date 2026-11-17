import { EnvironmentProviders, Provider } from '@angular/core';

/**
 * Default (server-backed) environment. `providers` are spread into the app config; the `mock`
 * build configuration replaces this file with `environment.mock.ts`, so the mock API (and its
 * permutation algorithms) is only ever bundled in mock builds.
 */
export const environment: { providers: (Provider | EnvironmentProviders)[] } = {
  providers: [],
};
