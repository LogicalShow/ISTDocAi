import { isDevMode } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

// Keep browser console output available locally, but silence application and
// third-party console calls in optimized production builds.
if (!isDevMode()) {
  const noop = (): void => undefined;

  Object.assign(console, {
    debug: noop,
    error: noop,
    info: noop,
    log: noop,
    warn: noop,
  });
}

bootstrapApplication(AppComponent, appConfig)
  .catch((err) => console.error(err));
