import { Component, OnInit, inject, signal, DestroyRef, computed } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FeedbackService } from '../../../../../core/services/feedback.service';
import { TranslationService } from '../../../../../core/services/translation.service';
import { PermissionEditorComponent } from '../../../components/permission-editor/permission-editor.component';
import { RolePermissionsDto, RolePermissionsService } from '../../../services/role-permissions.service';

@Component({
  selector: 'app-role-permissions',
  imports: [PermissionEditorComponent],
  templateUrl: './role-permissions.component.html',
})
export class RolePermissionsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private router = inject(Router);
  private rolePermissionsService = inject(RolePermissionsService);
  private feedbackService = inject(FeedbackService);
  private translationService = inject(TranslationService);

  readonly roleId = signal('');
  readonly permissions = signal<RolePermissionsDto | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);

  private selectedPageActionIds = new Set<string>();

  ngOnInit(): void {
    this.route.params.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      this.roleId.set(params['id']);
      if (this.roleId()) {
        this.loadPermissions();
      }
    });
  }

  onSelectionChange(ids: string[]): void {
    this.selectedPageActionIds = new Set(ids);
  }

  readonly roleName = computed((): string => {
    return this.resolveName(
      this.permissions()?.RoleNameEn,
      this.permissions()?.RoleNameAr,
      this.roleId()
    );
  });

  save(): void {
    if (!this.roleId() || this.saving()) return;

    this.saving.set(true);
    this.rolePermissionsService.assignRolePermissions({
      RoleId: this.roleId(),
      PageActionIds: Array.from(this.selectedPageActionIds)
    }).subscribe({
      next: () => {
        this.feedbackService.success(
          'feedback.permissions.roleUpdated.title',
          'feedback.permissions.roleUpdated.message'
        );
        this.saving.set(false);
        this.cancel();
      },
      error: error => {
        console.error('Error saving permissions:', error);
        this.feedbackService.handleApiError(error);
        this.saving.set(false);
      }
    });
  }

  cancel(): void {
    this.router.navigate(['/iam/roles']);
  }

  private loadPermissions(): void {
    this.loading.set(true);
    this.rolePermissionsService.getRolePermissions(this.roleId()).subscribe({
      next: data => {
        this.permissions.set(data);
        this.loading.set(false);
      },
      error: error => {
        console.error('Error loading permissions:', error);
        this.feedbackService.handleApiError(error);
        this.loading.set(false);
      }
    });
  }

  private resolveName(nameEn?: string, nameAr?: string, fallback = 'Not available'): string {
    const preferred = this.translationService.culture() === 'ar' ? nameAr : nameEn;
    const secondary = this.translationService.culture() === 'ar' ? nameEn : nameAr;
    return preferred?.trim() || secondary?.trim() || fallback;
  }
}
