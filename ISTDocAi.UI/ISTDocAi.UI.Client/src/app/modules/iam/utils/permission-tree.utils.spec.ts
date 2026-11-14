import { describe, expect, it } from 'vitest';
import { collectAssignedPageActionIds } from './permission-tree.utils';
import { ApplicationModuleDto, ModuleDto, PageDto } from '../models/permission-tree.model';

function page(id: string, assigned: boolean, children: PageDto[] = []): PageDto {
  return {
    Id: id, NameEn: id, NameAr: id, ChildPages: children,
    Actions: [{ Id: `${id}.VIEW`, ActionId: 'VIEW', ActionNameEn: 'View',
      ActionNameAr: 'View', PermissionKey: `${id}.VIEW`, IsAssigned: assigned }]
  };
}
function moduleOf(pages: PageDto[], children: ModuleDto[] = [], direct: PageDto | null = null): ModuleDto {
  return { Id: 'ADM', NameEn: 'Administration', NameAr: 'Administration',
    Pages: pages, Children: children, DirectPage: direct };
}
function application(modules: ModuleDto[]): ApplicationModuleDto[] {
  return [{ Id: 'APP', NameEn: 'Application', NameAr: 'Application', Modules: modules }];
}

describe('permission assignments', () => {
  it('excludes unassigned actions while retaining assignments on nested pages and modules', () => {
    const tree = application([moduleOf(
      [page('parent', false, [page('child', true)])],
      [moduleOf([page('nested-module', true), page('unassigned', false)])]
    )]);
    expect(collectAssignedPageActionIds(tree)).toEqual(['child.VIEW', 'nested-module.VIEW']);
  });

  it('uses a direct page once, even when a server response also includes it in Pages', () => {
    const direct = page('direct', true);
    expect(collectAssignedPageActionIds(application([moduleOf([direct], [], direct)])))
      .toEqual(['direct.VIEW']);
  });

  it('returns no grants for an empty tree or an application without modules', () => {
    expect(collectAssignedPageActionIds([])).toEqual([]);
    expect(collectAssignedPageActionIds(application([]))).toEqual([]);
  });
});
