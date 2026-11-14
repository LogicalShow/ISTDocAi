import { NgTemplateOutlet } from '@angular/common';
import { Component, DestroyRef, OnChanges, SimpleChanges, WritableSignal, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { TranslatePipe } from '@ngx-translate/core';
import { NzInputModule } from 'ng-zorro-antd/input';
import { FeedbackService } from '../../../../core/services/feedback.service';
import { AppStore } from '../../../../core/state/app.store';
import { ButtonComponent } from '../../../../shared/components/button/button.component';
import { IconComponent } from '../../../../shared/icons/icon.component';
import {
  ApplicationModuleDto,
  LocalizedApplicationModuleDto,
  LocalizedModuleDto,
  LocalizedPageActionDto,
  LocalizedPageDto
} from '../../models/permission-tree.model';
import { PermissionTreeLocalizationService } from '../../services/permission-tree-localization.service';

@Component({
  selector: 'app-permission-tree',
  imports: [NgTemplateOutlet, FormsModule, NzInputModule, TranslatePipe, ButtonComponent, IconComponent],
  templateUrl: './permission-tree.component.html',
  styleUrl: './permission-tree.component.css'
})
export class PermissionTreeComponent implements OnChanges {
  private feedbackService = inject(FeedbackService);
  private appStore = inject(AppStore);
  private localizationService = inject(PermissionTreeLocalizationService);

  readonly applications = input.required<ApplicationModuleDto[]>();
  readonly importAction = input<(() => void) | null>(null);
  readonly importDisabled = input(false);
  readonly importing = input(false);
  /** Selection pushed in from outside (import from role). A new array instance re-applies it. */
  readonly importedSelection = input<readonly string[] | null>(null);

  readonly selectionChange = output<string[]>();

  readonly localizedApplications = signal<LocalizedApplicationModuleDto[]>([]);
  readonly searchTerm = signal('');
  /** Working copy the tree operations mutate; `selectedIds` is the published
   *  snapshot the template reads, so every change re-renders the tree. */
  private readonly selectedPageActionIds = new Set<string>();
  private readonly selectedIds = signal<ReadonlySet<string>>(new Set());
  readonly collapsedApplications = signal<ReadonlySet<string>>(new Set());
  readonly collapsedModules = signal<ReadonlySet<string>>(new Set());
  readonly collapsedPages = signal<ReadonlySet<string>>(new Set());

  constructor() {
    // Fires outside this template (language switch), so the rebuild has to
    // land in a signal to refresh the view.
    this.appStore.cultureChanged$
      .pipe(takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => this.rebuildLocalizedApplications(true));
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['applications']) {
      this.rebuildLocalizedApplications(false);
      this.syncSelection();
      this.initializeExpansionState();
    }

    if (changes['importedSelection']) {
      const importedIds = this.importedSelection();
      if (importedIds) {
        this.applyImportedSelection(importedIds);
      }
    }
  }

  selectAll(): void {
    this.localizedApplications().forEach(application => this.setApplicationAssigned(application, true));
    this.emitSelection();
    this.feedbackService.success(
      'feedback.permissions.allSelected.title',
      'feedback.permissions.allSelected.message'
    );
  }

  clearAll(): void {
    this.feedbackService.confirm(
      'feedback.permissions.deselectAllConfirmation.title',
      'feedback.permissions.deselectAllConfirmation.message',
      () => {
        this.localizedApplications().forEach(application => this.setApplicationAssigned(application, false));
        this.emitSelection();
        this.feedbackService.success(
          'feedback.permissions.allDeselected.title',
          'feedback.permissions.allDeselected.message'
        );
        return true;
      },
      { okLabelKey: 'permissionsPage.clearAll', destructive: true }
    );
  }

  runImport(): void {
    if (this.importing()) {
      return;
    }

    this.importAction()?.();
  }

  toggleAction(action: LocalizedPageActionDto): void {
    action.IsAssigned = !action.IsAssigned;
    this.updateSelectionId(action);
    this.emitSelection();
  }

  togglePage(page: LocalizedPageDto): void {
    this.setPageTreeAssigned(page, !this.isPageAllSelected(page));
    this.emitSelection();
  }

  toggleModule(module: LocalizedModuleDto): void {
    this.setModuleTreeAssigned(module, !this.isModuleAllSelected(module));
    this.emitSelection();
  }

  toggleApplication(application: LocalizedApplicationModuleDto): void {
    this.setApplicationAssigned(application, !this.isApplicationAllSelected(application));
    this.emitSelection();
  }

  isApplicationAllSelected(application: LocalizedApplicationModuleDto): boolean {
    return this.allAssigned(application.Modules.flatMap(module => this.getModuleTreeActions(module)));
  }

  isModuleAllSelected(module: LocalizedModuleDto): boolean {
    return this.allAssigned(this.getModuleTreeActions(module));
  }

  isPageAllSelected(page: LocalizedPageDto): boolean {
    return this.allAssigned(this.getPageTreeActions(page));
  }

  isApplicationVisible(application: LocalizedApplicationModuleDto): boolean {
    const term = this.normalizedSearchTerm;
    if (!term) return true;
    return application.Name.toLowerCase().includes(term) ||
      application.Modules.some(module => this.isModuleVisible(module));
  }

  isModuleVisible(module: LocalizedModuleDto): boolean {
    const term = this.normalizedSearchTerm;
    if (!term) return true;
    return module.Name.toLowerCase().includes(term) ||
      this.getModuleOwnPages(module).some(page => this.isPageVisible(page)) ||
      (module.Children ?? []).some(child => this.isModuleVisible(child));
  }

  isPageVisible(page: LocalizedPageDto): boolean {
    const term = this.normalizedSearchTerm;
    if (!term) return true;
    return page.Name.toLowerCase().includes(term) ||
      this.getChildPages(page).some(child => this.isPageVisible(child));
  }

  isApplicationExpanded(application: LocalizedApplicationModuleDto): boolean {
    return !!this.normalizedSearchTerm || !this.collapsedApplications().has(application.Id);
  }

  isModuleExpanded(module: LocalizedModuleDto): boolean {
    return !!this.normalizedSearchTerm || !this.collapsedModules().has(module.Id);
  }

  isPageExpanded(page: LocalizedPageDto): boolean {
    return !!this.normalizedSearchTerm || !this.collapsedPages().has(page.Id);
  }

  isActionAssigned(action: LocalizedPageActionDto): boolean {
    return this.selectedIds().has(action.Id);
  }

  toggleApplicationExpansion(application: LocalizedApplicationModuleDto): void {
    this.toggleCollapsed(this.collapsedApplications, application.Id);
  }

  toggleModuleExpansion(module: LocalizedModuleDto): void {
    this.toggleCollapsed(this.collapsedModules, module.Id);
  }

  togglePageExpansion(page: LocalizedPageDto): void {
    if (this.hasChildPages(page)) {
      this.toggleCollapsed(this.collapsedPages, page.Id);
    }
  }

  getModuleOwnPages(module: LocalizedModuleDto): LocalizedPageDto[] {
    return module.DirectPage ? [module.DirectPage] : (module.Pages ?? []);
  }

  getChildPages(page: LocalizedPageDto): LocalizedPageDto[] {
    return page.ChildPages ?? [];
  }

  hasChildPages(page: LocalizedPageDto): boolean {
    return this.getChildPages(page).length > 0;
  }

  trackById(_index: number, item: { Id: string }): string {
    return item.Id;
  }

  private get normalizedSearchTerm(): string {
    return this.searchTerm().trim().toLowerCase();
  }

  private rebuildLocalizedApplications(preserveSelection: boolean): void {
    const localizedApplications = this.localizationService.localizeApplications(this.applications());

    if (preserveSelection) {
      localizedApplications.forEach(application => {
        application.Modules.forEach(module => {
          this.getModuleTreeActions(module).forEach(action => {
            action.IsAssigned = this.selectedPageActionIds.has(action.Id);
          });
        });
      });
    }

    this.localizedApplications.set(localizedApplications);
  }

  private syncSelection(): void {
    this.selectedPageActionIds.clear();
    this.localizedApplications().forEach(application => {
      application.Modules.forEach(module => {
        this.getModuleTreeActions(module).forEach(action => this.updateSelectionId(action));
      });
    });
    this.emitSelection();
  }

  private initializeExpansionState(): void {
    const collapsedPages = new Set<string>();

    this.localizedApplications().forEach(application => {
      application.Modules.forEach(module => this.walkModule(module, currentModule => {
        this.getModuleOwnPages(currentModule).forEach(page => this.walkPage(page, currentPage => {
          if (this.hasChildPages(currentPage)) {
            collapsedPages.add(currentPage.Id);
          }
        }));
      }));
    });

    this.collapsedApplications.set(new Set());
    this.collapsedModules.set(new Set());
    this.collapsedPages.set(collapsedPages);
  }

  /** Replaces the whole selection with `ids`, same local-only contract as `selectAll`. */
  private applyImportedSelection(ids: readonly string[]): void {
    const imported = new Set(ids);

    this.localizedApplications().forEach(application => {
      application.Modules.forEach(module => {
        this.getModuleTreeActions(module).forEach(action => {
          action.IsAssigned = imported.has(action.Id);
          this.updateSelectionId(action);
        });
      });
    });

    this.emitSelection();
  }

  private emitSelection(): void {
    this.selectedIds.set(new Set(this.selectedPageActionIds));
    this.selectionChange.emit(Array.from(this.selectedPageActionIds));
  }

  private setApplicationAssigned(application: LocalizedApplicationModuleDto, assigned: boolean): void {
    application.Modules.forEach(module => this.setModuleTreeAssigned(module, assigned));
  }

  private setModuleTreeAssigned(module: LocalizedModuleDto, assigned: boolean): void {
    this.getModuleOwnPages(module).forEach(page => this.setPageTreeAssigned(page, assigned));
    (module.Children ?? []).forEach(child => this.setModuleTreeAssigned(child, assigned));
  }

  private setPageTreeAssigned(page: LocalizedPageDto, assigned: boolean): void {
    this.getPageTreeActions(page).forEach(action => {
      action.IsAssigned = assigned;
      this.updateSelectionId(action);
    });
  }

  private updateSelectionId(action: LocalizedPageActionDto): void {
    if (action.IsAssigned) {
      this.selectedPageActionIds.add(action.Id);
    } else {
      this.selectedPageActionIds.delete(action.Id);
    }
  }

  private getModuleTreeActions(module: LocalizedModuleDto): LocalizedPageActionDto[] {
    return [
      ...this.getModuleOwnPages(module).flatMap(page => this.getPageTreeActions(page)),
      ...(module.Children ?? []).flatMap(child => this.getModuleTreeActions(child))
    ];
  }

  private getPageTreeActions(page: LocalizedPageDto): LocalizedPageActionDto[] {
    return [
      ...(page.Actions ?? []),
      ...this.getChildPages(page).flatMap(child => this.getPageTreeActions(child))
    ];
  }

  private allAssigned(actions: LocalizedPageActionDto[]): boolean {
    const selected = this.selectedIds();
    return actions.length > 0 && actions.every(action => selected.has(action.Id));
  }

  private toggleCollapsed(collection: WritableSignal<ReadonlySet<string>>, id: string): void {
    collection.update(current => {
      const next = new Set(current);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  private walkModule(module: LocalizedModuleDto, callback: (module: LocalizedModuleDto) => void): void {
    callback(module);
    (module.Children ?? []).forEach(child => this.walkModule(child, callback));
  }

  private walkPage(page: LocalizedPageDto, callback: (page: LocalizedPageDto) => void): void {
    callback(page);
    this.getChildPages(page).forEach(child => this.walkPage(child, callback));
  }
}
