import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiHttpService } from './api-http.service';
import { TranslationService } from './translation.service';
import { UserMenuDto, MenuModuleDto, MenuPageDto } from '../models/user-menu.model';
import { SidebarItem, SidebarChildItem } from '../models/sidebar-item.model';
import { IconName } from '../../shared/icons/app-icons';
@Injectable({ providedIn: 'root' })
export class UserMenuService {
  private httpService = inject(ApiHttpService);
  private translationService = inject(TranslationService);


  getUserMenu(): Observable<UserMenuDto> {
    return this.httpService.get<UserMenuDto>('/api/IAM/UserMenu');
  }

  localizeMenu(userMenuSource: UserMenuDto): SidebarItem[] {
    return this.transformToSidebarItems(userMenuSource, this.translationService.culture());
  }




  private transformToSidebarItems(userMenuSource: UserMenuDto, culture: string): SidebarItem[] {
    return userMenuSource.Modules.map(module => this.transformModule(module, culture));
  }

  private transformModule(module: MenuModuleDto, culture: string): SidebarItem {
    return {
      title: this.resolveName(module, culture, module.Id),
      icon: module.Icon as IconName,
      url: module.Route ?? undefined,
      openInNewTab: module.OpenInNewTab,
      children: module.Route ? [] : this.buildChildren(module, culture)
    };
  }

  // Recursive helper to build children list (Pages + SubModules merged by DisplayOrder)
  private buildChildren(module: MenuModuleDto, culture: string): SidebarChildItem[] {
    // Pages -> SidebarChildItem with DisplayOrder
    const pageItems: (SidebarChildItem & { displayOrder: number })[] = module.Pages.map(page => ({
      label: this.resolveName(page, culture, page.Id, page.Route),
      url: page.Route,
      openInNewTab: page.OpenInNewTab,
      displayOrder: page.DisplayOrder
    }));

    // SubModules -> SidebarChildItem (recursive) with DisplayOrder
    const subModuleItems: (SidebarChildItem & { displayOrder: number })[] = module.SubModules.map(sub => ({
      label: this.resolveName(sub, culture, sub.Id),
      url: sub.Route ?? undefined,
      openInNewTab: sub.OpenInNewTab,
      children: sub.Route ? undefined : this.buildChildren(sub, culture),
      displayOrder: sub.DisplayOrder
    }));

    // Merge and sort by DisplayOrder
    const merged = [...pageItems, ...subModuleItems].sort((a, b) => a.displayOrder - b.displayOrder);
    
    // Return the public sidebar shape without the temporary sorting field.
    return merged.map(item => ({
      label: item.label,
      url: item.url,
      openInNewTab: item.openInNewTab,
      children: item.children
    }));
  }

  private resolveName(
    item: Pick<MenuModuleDto | MenuPageDto, 'NameEn' | 'NameAr'>,
    culture: string,
    ...fallbackValues: (string | null | undefined)[]
  ): string {
    const preferred = culture === 'ar' ? item.NameAr : item.NameEn;
    const secondary = culture === 'ar' ? item.NameEn : item.NameAr;

    return preferred?.trim() ||
      secondary?.trim() ||
      fallbackValues.find(value => value?.trim())?.trim() ||
      'Not available';
  }
}
