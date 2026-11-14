import { ApplicationModuleDto, ModuleDto, PageDto } from '../models/permission-tree.model';

/**
 * Flattens a permission tree down to the ids of the page actions marked as assigned.
 * `DirectPage` and `Pages` are mutually exclusive, so only one of them ever carries pages.
 */
export function collectAssignedPageActionIds(applications: ApplicationModuleDto[]): string[] {
  const ids: string[] = [];
  applications.forEach(application => {
    (application.Modules ?? []).forEach(module => collectFromModule(module, ids));
  });
  return ids;
}

function collectFromModule(module: ModuleDto, ids: string[]): void {
  const ownPages = module.DirectPage ? [module.DirectPage] : (module.Pages ?? []);
  ownPages.forEach(page => collectFromPage(page, ids));
  (module.Children ?? []).forEach(child => collectFromModule(child, ids));
}

function collectFromPage(page: PageDto, ids: string[]): void {
  (page.Actions ?? []).forEach(action => {
    if (action.IsAssigned) {
      ids.push(action.Id);
    }
  });
  (page.ChildPages ?? []).forEach(child => collectFromPage(child, ids));
}
