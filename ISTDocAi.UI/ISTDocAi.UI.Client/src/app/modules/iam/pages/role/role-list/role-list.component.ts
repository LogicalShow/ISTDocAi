import { Component, OnInit, inject, signal, DestroyRef, computed } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule } from '@angular/forms';

import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonComponent } from '../../../../../shared/components/button/button.component';
import { IconComponent } from '../../../../../shared/icons/icon.component';
import { GetListResult } from '../../../../../shared/models/get-list-result.model';
import { RoleService } from '../../../services/role.service';
import { RoleGetListQuery, RoleListItem, RoleDeleteCommand } from "../../../models/role-list-item.model";
import { RoleEditComponent } from '../role-edit/role-edit.component';
import { ModalManagementService } from '../../../../../core/services/modal-management.service';
import { FeedbackService } from '../../../../../core/services/feedback.service';
import { RoleSearchComponent } from '../role-search/role-search.component';
import { trimObjectStrings } from "../../../../../shared/utils/object-utils";
import { PermissionService } from '../../../../../core/services/permission.service';
import { PERMISSIONS } from '../../../../../core/constants/permissions.constants';
import { PermissionControlDirective } from '../../../../../core/directives/permission-control.directive';

type PageEntry = number | 'ellipsis';

@Component({
  selector: 'app-role-list',
  templateUrl: './role-list.component.html',
  styleUrl: './role-list.component.css',
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
export class RoleListComponent implements OnInit {
  private fb = inject(FormBuilder);
  private roleService = inject(RoleService);
  private destroyRef = inject(DestroyRef);
  private modalManagementService = inject(ModalManagementService);
  private feedbackService = inject(FeedbackService);
  private router = inject(Router);
  private permissionService = inject(PermissionService);

  readonly PERMISSIONS = PERMISSIONS.ROLE;

  searchForm: FormGroup;
  readonly roles = signal<RoleListItem[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  sortField = 'NameEn';
  sortOrder: 'ascend' | 'descend' | null = null;
  isEditing = false; // prevent opening multiple edit modals
  readonly checkedIds = signal<ReadonlySet<string>>(new Set<string>());
  readonly selectAllChecked = signal(false);

  readonly canAdd = computed(() => this.permissionService.hasPermission(PERMISSIONS.ROLE.ADD));
  readonly canEdit = computed(() => this.permissionService.hasPermission(PERMISSIONS.ROLE.EDIT));
  readonly canDelete = computed(() => this.permissionService.hasPermission(PERMISSIONS.ROLE.DELETE));

  constructor() {
    this.searchForm = this.fb.group({
      name: ['']
    });

  }

  ngOnInit(): void {
    this.roleService.added$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.loadData();
    });
    this.loadData();
  }

  loadData() {
    this.loading.set(true);
    const trimmedFormValues = trimObjectStrings(this.searchForm.value);
    const query: RoleGetListQuery = {
      Name: trimmedFormValues.name || undefined,
      SortField: this.sortField || undefined,
      SortOrder: this.sortOrder === 'ascend' ? 'asc' : this.sortOrder === 'descend' ? 'desc' : undefined,
      PageNo: this.pageIndex(),
      PageRowCount: this.pageSize()
    };
    this.roleService.getList(query).subscribe({
      next: (res: GetListResult<RoleListItem>) => {
        this.roles.set(res.Items);
        this.total.set(res.TotalCount);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading roles:', error);
        this.feedbackService.handleApiError(error);
        this.loading.set(false);
      }
    });
  }

  onSearch() {
    this.pageIndex.set(1);
    this.loadData();
  }

  clearSearch(): void {
    this.searchForm.reset({ name: '' });
    this.onSearch();
  }

  onSort(field: string): void {
    if (this.sortField !== field) {
      this.sortField = field;
      this.sortOrder = 'ascend';
    } else if (this.sortOrder === 'ascend') {
      this.sortOrder = 'descend';
    } else {
      this.sortField = 'NameEn';
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

  addRole() {
    this.modalManagementService.openAddModal(
      'role.entityName',
      RoleEditComponent,
      () => this.loadData(),
      undefined,
      485,
      'role-edit-modal'
    );
  }

  editRole(role: RoleListItem) {
    if (this.isEditing) return;
    this.isEditing = true;

    this.roleService.getById(role.Id).subscribe({
      next: (entity) => {
        const modalRef = this.modalManagementService.openEntityModal({
          title: 'role.entityName',
          component: RoleEditComponent,
          mode: this.canEdit() ? 'edit' : 'view',
          entity: entity,
          width: 485,
          nzClassName: 'role-edit-modal'
        }, () => {
          this.loadData();
        });

        modalRef.afterClose.subscribe(() => {
          this.isEditing = false;
        });
      },
      error: (error) => {
        console.error('Error loading role:', error);
        this.feedbackService.handleApiError(error);
        this.isEditing = false;
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

  deleteSelectedRoles(): void {
    if (this.checkedIds().size === 0) {
      this.feedbackService.info(
        'feedback.role.selectionRequired.title',
        'feedback.role.selectionRequired.message'
      );
      return;
    }

    const count = this.checkedIds().size;
    this.feedbackService.confirm(
      'feedback.role.deleteConfirmation.title',
      count === 1
        ? 'feedback.role.deleteConfirmation.messageOne'
        : 'feedback.role.deleteConfirmation.message',
      () => {
        const itemsToDelete = this.roles()
          .filter(item => this.checkedIds().has(item.Id))
          .map(item => ({
            Id: item.Id,
            ConcurrencyStamp: item.ConcurrencyStamp
          }));

        const command: RoleDeleteCommand = {
          items: itemsToDelete
        };

        this.roleService.delete(command).subscribe({
          next: () => {
            this.feedbackService.success('feedback.role.deleted.title', 'feedback.role.deleted.message');
            this.checkedIds.set(new Set<string>());
            this.selectAllChecked.set(false);
            this.loadData();
          },
          error: (err) => {
            console.error('Error deleting roles:', err);
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
      this.roles().forEach(item => next.add(item.Id));
    } else {
      this.roles().forEach(item => next.delete(item.Id));
    }
    this.checkedIds.set(next);
  }

  openSearchModal(): void {
    const modalRef = this.modalManagementService.openSearchModal(
      'role.search.title',
      RoleSearchComponent,
      this.searchForm.value,
      'role-search-modal'
    );
  
    modalRef.afterClose.subscribe((result) => {
      if (result) {
        this.searchForm.setValue(result);
        this.onSearch();
      }
    });
  }

  setPermissions(role: RoleListItem): void {
    this.router.navigate(['/iam/roles/permissions', role.Id]);
  }
}


