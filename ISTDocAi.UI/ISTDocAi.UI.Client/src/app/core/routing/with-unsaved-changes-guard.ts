import { Route, Routes } from '@angular/router';
import { unsavedChangesGuard } from '../guards/unsaved-changes.guard';

/** Attaches unsavedChangesGuard to every route that renders a component. */
export function withUnsavedChangesGuard(routes: Routes): Routes {
  return routes.map(route => {
    const guarded: Route = { ...route };

    if (route.component || route.loadComponent) {
      guarded.canDeactivate = [...(route.canDeactivate ?? []), unsavedChangesGuard];
    }

    if (route.children) {
      guarded.children = withUnsavedChangesGuard(route.children);
    }

    return guarded;
  });
}
