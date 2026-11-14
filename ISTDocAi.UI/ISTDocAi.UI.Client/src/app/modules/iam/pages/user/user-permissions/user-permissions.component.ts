import { Component, OnInit, inject, signal, DestroyRef } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FeedbackService } from '../../../../../core/services/feedback.service';
import { PermissionEditorComponent } from '../../../components/permission-editor/permission-editor.component';
import { UserPermissionsDto, UserPermissionsService } from '../../../services/user-permissions.service';
import { UserService } from '../../../services/user.service';
import { collectAssignedPageActionIds } from '../../../utils/permission-tree.utils';

@Component({
  selector: 'app-user-permissions',
  imports: [PermissionEditorComponent],
  templateUrl: './user-permissions.component.html',
})
export class UserPermissionsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private destroyRef = inject(DestroyRef);
  private router = inject(Router);
  private userPermissionsService = inject(UserPermissionsService);
  private userService = inject(UserService);
  private feedbackService = inject(FeedbackService);

  readonly userId = signal('');
  readonly userName = signal('');
  readonly userRoleId = signal<string | null>(null);
  readonly permissions = signal<UserPermissionsDto | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly importing = signal(false);
  /** Role permissions staged into the tree; only `save()` persists them. */
  readonly importedSelection = signal<readonly string[] | null>(null);
  readonly importedFromRoleId = signal<string | null>(null);

  readonly importFromRoleAction = (): void => this.importFromRole();

  private selectedPageActionIds = new Set<string>();

  ngOnInit(): void {
    this.route.params.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      this.userId.set(params['id']);
      if (this.userId()) {
        this.loadUserData();
        this.loadPermissions();
      }
    });
  }

  onSelectionChange(ids: string[]): void {
    this.selectedPageActionIds = new Set(ids);
  }

  save(): void {
    if (!this.userId() || this.saving()) return;

    this.saving.set(true);
    this.userPermissionsService.assignUserPermissions({
      UserId: this.userId(),
      PageActionIds: Array.from(this.selectedPageActionIds),
      SourceRoleId: this.importedFromRoleId()
    }).subscribe({
      next: () => {
        this.feedbackService.success(
          'feedback.permissions.userUpdated.title',
          'feedback.permissions.userUpdated.message'
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
    this.router.navigate(['/iam/users']);
  }

  private loadUserData(): void {
    this.userService.getById(this.userId()).subscribe({
      next: user => {
        this.userRoleId.set(user.RoleId || null);
      },
      error: error => {
        console.error('Error loading user data:', error);
        this.feedbackService.handleApiError(error);
      }
    });
  }

  private loadPermissions(): void {
    this.loading.set(true);
    this.userPermissionsService.getUserPermissions(this.userId()).subscribe({
      next: data => {
        this.permissions.set(data);
        this.userName.set(data.UserName);
        this.loading.set(false);
      },
      error: error => {
        console.error('Error loading permissions:', error);
        this.feedbackService.handleApiError(error);
        this.loading.set(false);
      }
    });
  }

  private importFromRole(): void {
    if (this.importing()) {
      return;
    }

    if (!this.userId() || !this.userRoleId()) {
      this.feedbackService.warning(
        'feedback.permissions.userRoleRequired.title',
        'feedback.permissions.userRoleRequired.message'
      );
      return;
    }

    this.feedbackService.confirm(
      'feedback.permissions.importConfirmation.title',
      'feedback.permissions.importConfirmation.message',
      () => {
        this.importing.set(true);

        this.userPermissionsService.getRolePermissionsForImport(this.userRoleId()!).subscribe({
          next: rolePermissions => {
            // Staged into the tree only - nothing is persisted until Save.
            this.importedFromRoleId.set(this.userRoleId());
            this.importedSelection.set(collectAssignedPageActionIds(rolePermissions.Applications));
            this.feedbackService.success(
              'feedback.permissions.importedFromRole.title',
              'feedback.permissions.importedFromRole.message'
            );
            this.importing.set(false);
          },
          error: error => {
            console.error('Error importing permissions:', error);
            this.feedbackService.handleApiError(error);
            this.importing.set(false);
          }
        });
        return true;
      },
      { okLabelKey: 'common.import' }
    );
  }
}
