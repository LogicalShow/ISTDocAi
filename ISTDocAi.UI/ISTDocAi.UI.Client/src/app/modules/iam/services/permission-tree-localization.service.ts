import { Injectable, inject } from '@angular/core';
import { TranslationService } from '../../../core/services/translation.service';
import {
  ApplicationModuleDto,
  LocalizedApplicationModuleDto,
  LocalizedModuleDto,
  LocalizedPageActionDto,
  LocalizedPageDto,
  ModuleDto,
  PageActionDto,
  PageDto
} from '../models/permission-tree.model';

@Injectable({ providedIn: 'root' })
export class PermissionTreeLocalizationService {
  private translationService = inject(TranslationService);


  localizeApplications(applications: ApplicationModuleDto[]): LocalizedApplicationModuleDto[] {
    return applications.map(application => ({
      ...application,
      Name: this.resolveName(application.NameEn, application.NameAr, application.Id),
      Modules: application.Modules.map(module => this.localizeModule(module))
    }));
  }

  private localizeModule(module: ModuleDto): LocalizedModuleDto {
    return {
      ...module,
      Name: this.resolveName(module.NameEn, module.NameAr, module.Id),
      DirectPage: module.DirectPage ? this.localizePage(module.DirectPage) : null,
      Pages: (module.Pages ?? []).map(page => this.localizePage(page)),
      Children: (module.Children ?? []).map(child => this.localizeModule(child))
    };
  }

  private localizePage(page: PageDto): LocalizedPageDto {
    return {
      ...page,
      Name: this.resolveName(page.NameEn, page.NameAr, page.Id),
      Actions: (page.Actions ?? []).map(action => this.localizeAction(action)),
      ChildPages: (page.ChildPages ?? []).map(child => this.localizePage(child))
    };
  }

  private localizeAction(action: PageActionDto): LocalizedPageActionDto {
    return {
      ...action,
      ActionName: this.resolveName(action.ActionNameEn, action.ActionNameAr, action.ActionId)
    };
  }

  private resolveName(nameEn?: string, nameAr?: string, fallback = 'Not available'): string {
    const culture = this.translationService.culture();
    const preferred = culture === 'ar' ? nameAr : nameEn;
    const secondary = culture === 'ar' ? nameEn : nameAr;
    return preferred?.trim() || secondary?.trim() || fallback;
  }
}
