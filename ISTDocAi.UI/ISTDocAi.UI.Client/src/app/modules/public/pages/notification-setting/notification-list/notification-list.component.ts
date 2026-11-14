import { Component, OnInit, inject, signal } from '@angular/core';

import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTableModule, NzTableQueryParams } from 'ng-zorro-antd/table';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { ModalManagementService } from '../../../../../core/services/modal-management.service';
import { FeedbackService } from '../../../../../core/services/feedback.service';
import { NotificationService } from '../../../services/notification.service';
import { NotificationListItem, NotificationGetListQuery } from '../../../models/notification.model';
import { SearchToolbarComponent } from '../../../../../shared/pages/search-toolbar/search-toolbar.component';
import { NotificationSearchComponent } from '../notification-search/notification-search.component';
import { NotificationUserEditComponent } from '../notification-user-edit/notification-user-edit.component';
import { trimObjectStrings } from '../../../../../shared/utils/object-utils';
import { ButtonComponent } from '../../../../../shared/components/button/button.component';

interface NotificationSearchCriteria {
  name?: string;
  generalMessageEn?: string;
}

@Component({
  selector: 'app-notification-list',
  templateUrl: './notification-list.component.html',
  styleUrls: ['./notification-list.component.css'],
  imports: [
    FormsModule,
    ReactiveFormsModule,
    NzFormModule,
    NzInputModule,
    NzTableModule,
    NzCheckboxModule,
    SearchToolbarComponent,
    ButtonComponent
],
})
export class NotificationListComponent implements OnInit {
  private fb = inject(FormBuilder);
  private notificationService = inject(NotificationService);
  private modalManagementService = inject(ModalManagementService);
  private feedback = inject(FeedbackService);

  searchForm: FormGroup;

  readonly notifications = signal<NotificationListItem[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);

  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  sortField = 'Name';
  sortOrder: 'ascend' | 'descend' | undefined = 'ascend';
  generalSearch = false;
  searchText?: string;
  readonly quickSearchText = signal<string | undefined>(undefined);

  constructor() {
    this.searchForm = this.fb.group({
      name: [''],
      generalMessageEn: [''],
    });
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);

    const v = trimObjectStrings(this.searchForm.value);
    const query: NotificationGetListQuery = {
      Name: v.name || undefined,
      GeneralMessageEn: v.generalMessageEn || undefined,
      SortBy: this.sortField || undefined,
      SortDirection: this.sortOrder === 'ascend' ? 'asc' : this.sortOrder === 'descend' ? 'desc' : undefined,
      PageNo: this.pageIndex(),
      PageRowCount: this.pageSize(),
      GeneralSearch: this.generalSearch,
      SearchText: this.searchText || undefined,
    };

    this.notificationService.getList(query).subscribe({
      next: (res) => {
        this.notifications.set(res.Items ?? []);
        this.total.set(res.TotalCount ?? 0);
        this.loading.set(false);
      },
      error: (err) => {
        this.feedback.handleApiError(err);
        this.notifications.set([]);
        this.total.set(0);
        this.loading.set(false);
      },
    });
  }

  onSearch(): void {
    this.pageIndex.set(1);
    this.generalSearch = false;
    this.loadData();
  }

  onQueryParamsChange(params: NzTableQueryParams): void {
    const { pageIndex, pageSize, sort } = params;
    const currentSort = sort.find((s) => s.value !== null);
    this.sortField = (currentSort?.key as string) ?? 'Name';
    this.sortOrder = (currentSort?.value as 'ascend' | 'descend') ?? 'ascend';
    this.pageIndex.set(pageIndex);
    this.pageSize.set(pageSize);
    this.loadData();
  }

  onAdd(): void {
    const modalRef = this.modalManagementService.openEntityModal({
      title: 'Notification User',
      component:NotificationUserEditComponent,
      mode: 'add',    
entity: {
        onSave: () => {
          this.loadData();
        }
      }
    });

     modalRef.afterClose.subscribe((result: string | undefined) => {
      if (result === 'success') {
        this.loadData();
      }
    });
  }

  viewNotification(item: NotificationListItem): void {
    if (!item.Id) return;

    this.notificationService.getById(item.Id).subscribe({
      next: (result) => {
        if (result) {
          this.modalManagementService.openEntityModal({
            title: 'Notification User',
            component: NotificationUserEditComponent,
            mode: 'edit',
            entity: result
          }, () => {
            this.loadData();
          });
        } else {
          this.feedback.error(
            'feedback.notificationSettings.detailsLoadFailed.title',
            'feedback.notificationSettings.detailsLoadFailed.message'
          );
        }
      },
      error: (err) => {
        this.feedback.handleApiError(err);
      }
    });
  }

  onClear(): void {
    this.searchForm.reset({
      name: '',
      generalMessageEn: '',
    });
    this.generalSearch = false;
    this.quickSearchText.set('');
    this.onSearch();
  }

  onQuickSearch(): void {
    const text = (this.quickSearchText() ?? '').trim();
    this.generalSearch = true;
    this.searchText = text || undefined;
    this.pageIndex.set(1);
    this.loadData();
  }

  openSearchModal(): void {
    const modalRef = this.modalManagementService.openSearchModal(
      'Search Notification',
      NotificationSearchComponent,
      this.searchForm.value
    );

    modalRef.afterClose.subscribe((result: NotificationSearchCriteria | undefined) => {
      if (result) {
        this.searchForm.patchValue(result);
        this.onSearch();
      }
    });
  }
}
