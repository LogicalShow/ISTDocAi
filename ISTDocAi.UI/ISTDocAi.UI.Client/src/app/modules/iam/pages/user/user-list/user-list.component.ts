import { Component, OnInit, inject, signal, DestroyRef, computed } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule } from '@angular/forms';

import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzModalService } from 'ng-zorro-antd/modal';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ButtonComponent } from '../../../../../shared/components/button/button.component';
import { IconComponent } from '../../../../../shared/icons/icon.component';
import { UserService } from '../../../services/user.service';
import { UserGetListQuery, UserListItem, UserDeleteCommand } from '../../../models/user-list-item.model';
import { UserChangePasswordComponent } from '../user-change-password/user-change-password.component';
import { UserEditComponent } from '../user-edit/user-edit.component';
import { ModalManagementService } from '../../../../../core/services/modal-management.service';
import { FeedbackService } from '../../../../../core/services/feedback.service';
import { UserSearchComponent } from '../user-search/user-search.component';
import { trimObjectStrings } from '../../../../../shared/utils/object-utils';
import { finalize } from 'rxjs';
import { PermissionService } from '../../../../../core/services/permission.service';
import { PERMISSIONS } from '../../../../../core/constants/permissions.constants';
import { PermissionControlDirective } from '../../../../../core/directives/permission-control.directive';

type PageEntry = number | 'ellipsis';

@Component({
  selector: 'app-user-list',
  templateUrl: './user-list.component.html',
  styleUrl: './user-list.component.css',
  imports: [
    NzTableModule,
    NzCheckboxModule,
    FormsModule,
    PermissionControlDirective,
    TranslatePipe,
    ButtonComponent,
    IconComponent
]
})
export class UserListComponent implements OnInit {
  private fb = inject(FormBuilder);
  private userService = inject(UserService);
  private destroyRef = inject(DestroyRef);
  private modalManagementService = inject(ModalManagementService);
  private modal = inject(NzModalService);
  private feedbackService = inject(FeedbackService);
  private router = inject(Router);
  private permissionService = inject(PermissionService);
  private translate = inject(TranslateService);

  readonly PERMISSIONS = PERMISSIONS.USER;

  searchForm: FormGroup;
  readonly users = signal<UserListItem[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  sortField = 'UserName';
  sortOrder: 'ascend' | 'descend' | null = null;
  isEditing = false; // prevent opening multiple edit modals
  readonly checkedIds = signal<ReadonlySet<string>>(new Set<string>());
  readonly selectAllChecked = signal(false);

  readonly canAdd = computed(() => this.permissionService.hasPermission(PERMISSIONS.USER.ADD));
  readonly canEdit = computed(() => this.permissionService.hasPermission(PERMISSIONS.USER.EDIT));
  readonly canDelete = computed(() => this.permissionService.hasPermission(PERMISSIONS.USER.DELETE));

  constructor() {
    this.searchForm = this.fb.group({
      userName: [''],
      email: ['']
    });

  }

  ngOnInit(): void {
    this.userService.added$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.loadData();
    });
    this.loadData();
  }

  loadData() {
    this.loading.set(true);
    const trimmedFormValues = trimObjectStrings(this.searchForm.value);
    const query: UserGetListQuery = {
      UserName: trimmedFormValues.userName || undefined,
      Email: trimmedFormValues.email || undefined,
      SortField: this.sortField || undefined,
      SortOrder: this.sortOrder === 'ascend' ? 'asc' : this.sortOrder === 'descend' ? 'desc' : undefined,
      PageNo: this.pageIndex(),
      PageRowCount: this.pageSize()
    };
    this.userService.getList(query)
      .pipe(finalize(() => { this.loading.set(false); }))
      .subscribe({
        next: result => {
          this.users.set(result.Items);
          this.total.set(result.TotalCount);
        },
        error: (error) => {
          console.error('Error loading users:', error);
          this.feedbackService.handleApiError(error);
        }
      });
  }

  onSearch() {
    this.pageIndex.set(1);
    this.loadData();
  }

  clearSearch(): void {
    this.searchForm.reset({ userName: '', email: '' });
    this.onSearch();
  }

  onSort(field: string): void {
    if (this.sortField !== field) {
      this.sortField = field;
      this.sortOrder = 'ascend';
    } else if (this.sortOrder === 'ascend') {
      this.sortOrder = 'descend';
    } else {
      this.sortField = 'UserName';
      this.sortOrder = null;
    }
    this.pageIndex.set(1);
    this.loadData();
  }

  readonly totalPages = computed((): number => {
    return Math.max(1, Math.ceil(this.total() / this.pageSize()));
  });

  readonly pages = computed((): PageEntry[] => {
    const total = this.totalPages();
    const current = this.pageIndex();
    if (total <= 6) {
      return Array.from({ length: total }, (_, i) => i + 1);
    }
    if (current <= 4) {
      return [1, 2, 3, 4, 'ellipsis', total];
    }
    if (current >= total - 3) {
      return [1, 'ellipsis', total - 3, total - 2, total - 1, total];
    }
    return [1, 'ellipsis', current - 1, current, current + 1, 'ellipsis', total];
  });

  goToPage(page: PageEntry): void {
    if (page === 'ellipsis' || page === this.pageIndex()) {
      return;
    }
    this.pageIndex.set(page);
    this.loadData();
  }

  goToPrevious(): void {
    if (this.pageIndex() > 1) {
      this.pageIndex.update(v => v - 1);
      this.loadData();
    }
  }

  goToNext(): void {
    if (this.pageIndex() < this.totalPages()) {
      this.pageIndex.update(v => v + 1);
      this.loadData();
    }
  }

  addUser() {
    this.modalManagementService.openAddModal(
      'user.entityName',
      UserEditComponent,
      () => this.loadData(),
      undefined,
      485,
      'user-edit-modal'
    );
  }

  editUser(user: UserListItem) {
    if (this.isEditing) return;
    this.isEditing = true;

    this.userService.getById(user.Id)
      .pipe(finalize(() => { this.isEditing = false; }))
      .subscribe({
        next: user => {
          this.modalManagementService.openEntityModal({
            title: 'user.entityName',
            component: UserEditComponent,
            mode: this.canEdit() ? 'edit' : 'view',
            entity: user,
            width: 485,
            nzClassName: 'user-edit-modal'
          }, () => {
            this.loadData();
          });

        },
        error: (error) => {
          console.error('Error loading user:', error);
          this.feedbackService.handleApiError(error);
        }
      });
  }

  onCheckboxChange(id: string, checked: boolean): void {
    this.checkedIds.update(current => {
      const next = new Set(current);
      if (checked) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }

  isChecked(id: string): boolean {
    return this.checkedIds().has(id);
  }

  deleteSelectedUsers(): void {
    if (this.checkedIds().size === 0) {
      this.feedbackService.info(
        'feedback.user.selectionRequired.title',
        'feedback.user.selectionRequired.message'
      );
      return;
    }

    const count = this.checkedIds().size;
    this.feedbackService.confirm(
      'feedback.user.deleteConfirmation.title',
      count === 1
        ? 'feedback.user.deleteConfirmation.messageOne'
        : 'feedback.user.deleteConfirmation.message',
      () => {
        const itemsToDelete = this.users()
          .filter(item => this.checkedIds().has(item.Id))
          .map(item => ({
            Id: item.Id,
            ConcurrencyStamp: item.ConcurrencyStamp
          }));

        const command: UserDeleteCommand = {
          items: itemsToDelete
        };

        this.userService.delete(command).subscribe({
          next: () => {
            this.feedbackService.success('feedback.user.deleted.title', 'feedback.user.deleted.message');
            this.checkedIds.set(new Set<string>());
            this.selectAllChecked.set(false);
            this.loadData();
          },
          error: (err) => {
            console.error('Error deleting users:', err);
            this.feedbackService.handleApiError(err);
          }
        });

        return true;
      },
      { params: { count }, okLabelKey: 'common.delete', destructive: true }
    );
  }

  onSelectAllChange(): void {
    const next = new Set(this.checkedIds());
    if (this.selectAllChecked()) {
      this.users().forEach(item => next.add(item.Id));
    } else {
      this.users().forEach(item => next.delete(item.Id));
    }
    this.checkedIds.set(next);
  }

  openSearchModal(): void {
    const modalRef = this.modalManagementService.openSearchModal(
      'user.search.title',
      UserSearchComponent,
      this.searchForm.value,
      'user-search-modal'
    );

    modalRef.afterClose.subscribe((result) => {
      if (result) {
        this.searchForm.setValue(result);
        this.onSearch();
      }
    });
  }

  setPermissions(user: UserListItem): void {
    this.router.navigate(['/iam/users/permissions', user.Id]);
  }

  changePassword(user: UserListItem): void {
    // The component renders its own header, X and footer (Figma redesign), so
    // it bypasses ModalManagementService's ant footer buttons; the
    // unsaved-changes gate lives in the component's cancel() instead.
    const modalRef = this.modal.create<UserChangePasswordComponent>({
      nzContent: UserChangePasswordComponent,
      nzData: { entity: user },
      nzClassName: 'app-change-password-modal',
      nzWidth: 485,
      nzCentered: true,
      nzClosable: false,
      nzFooter: null,
      nzMaskClosable: false,
      // Escape routes through nzOnCancel: hand it to cancel() and block ant's
      // own close so the discard confirm can't be skipped.
      nzOnCancel: () => {
        modalRef.getContentComponent()?.cancel();
        return false;
      }
    });
  }
}
