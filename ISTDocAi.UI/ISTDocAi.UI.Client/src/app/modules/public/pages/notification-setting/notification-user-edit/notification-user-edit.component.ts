import { Component, OnInit, inject, signal, DestroyRef } from '@angular/core';

import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzSelectOptionInterface } from 'ng-zorro-antd/select';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzModalRef, NZ_MODAL_DATA } from 'ng-zorro-antd/modal';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SelectComponent } from '../../../../../shared/components/select/select.component';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { NotificationService } from '../../../services/notification.service';
import { NotificationUserService } from '../../../services/notification-user.service';
import { NotificationLookupItem, NotificationDetails } from '../../../models/notification.model';
import { NotificationUserUpsertCommandResponse, SelectedNotificationUser, UserGetListQueryResponse } from '../../../models/notification-user.model';
import { SaveableComponent } from '../../../../../core/models/modal-config.model';
import { HasUnsavedChanges } from '../../../../../core/models/has-unsaved-changes.model';
import { FeedbackService } from '../../../../../core/services/feedback.service';

// SendingWay enum options
export const SendingWayOptions = [
  { value: 1, label: 'System' },
  { value: 2, label: 'Email' },
  { value: 3, label: 'Both' }
];

interface UserRow {
  Id?: string;
  RowVersion?: string;
  UserId: string;
  UserName: string;
  SendingWayId: number;
  selected: boolean;
}

@Component({
  selector: 'app-notification-user-edit',
  templateUrl: './notification-user-edit.component.html',
  styleUrls: ['./notification-user-edit.component.css'],
  imports: [
    FormsModule,
    ReactiveFormsModule,
    NzFormModule,
    NzButtonModule,
    NzTableModule,
    NzCheckboxModule,
    SelectComponent
]
})
export class NotificationUserEditComponent implements OnInit, SaveableComponent, HasUnsavedChanges {
  private fb = inject(FormBuilder);
  private notificationService = inject(NotificationService);
  private notificationUserService = inject(NotificationUserService);
  private feedback = inject(FeedbackService);
  private modal = inject(NzModalRef);
  data = inject(NZ_MODAL_DATA);
  private destroyRef = inject(DestroyRef);

  form: FormGroup;
  mode: 'add' | 'edit';
  entity: NotificationDetails | null = null;

  // Notification dropdown
  notificationOptions: NotificationLookupItem[] = [];
  notificationSearchInput$ = new Subject<string>();
  readonly isLoadingNotifications = signal(false);
 onSaveCallback?: (item: NotificationUserUpsertCommandResponse) => void;
  // Users list
  readonly users = signal<UserRow[]>([]);
  allUsers: UserGetListQueryResponse[] = [];
  readonly isLoadingUsers = signal(false);
  /** Snapshot of `users` right after it's populated, to detect row edits
   * (selection/sending-way) that live outside `form` and so aren't covered
   * by `form.dirty`. */
  private initialUsersSnapshot = '';

  // SendingWay options
  sendingWayOptions = SendingWayOptions;

  readonly notificationSelectOptions = signal<NzSelectOptionInterface[]>([]);

  private syncNotificationSelectOptions(): void {
    this.notificationSelectOptions.set(this.notificationOptions.map(option => ({ value: option.Id, label: option.Name })));
  }

  constructor() {
    const data = this.data;

    this.mode = data?.mode ?? 'add';
    this.entity = data?.entity ?? null;
this.onSaveCallback = data?.entity?.onSave;
    this.form = this.fb.group({
      notificationId: [null, Validators.required],
    });
  }

  ngOnInit(): void {
    this.setupNotificationSearch();
    this.loadUsers();

    if (this.mode === 'edit' && this.entity) {
      this.populateForm();
    }
  }

  private setupNotificationSearch(): void {
    this.notificationSearchInput$
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) => {
          this.isLoadingNotifications.set(true);
          return this.notificationService.searchForSelect(term || '', 100);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (result) => {
          this.isLoadingNotifications.set(false);
          this.notificationOptions = result ?? [];
          this.syncNotificationSelectOptions();
        },
        error: () => {
          this.isLoadingNotifications.set(false);
        }
      });

    // Initial load
    this.notificationSearchInput$.next('');
  }

  private loadUsers(): void {
    this.isLoadingUsers.set(true);
    this.notificationUserService.getUserList().subscribe({
      next: (users) => {
        this.isLoadingUsers.set(false);
        this.allUsers = users ?? [];
        this.initializeUserRows();
      },
      error: (error) => {
        this.feedback.handleApiError(error);
        this.isLoadingUsers.set(false);
      }
    });
  }

  private initializeUserRows(): void {
    if (this.mode === 'edit' && this.entity) {
      // Combine selected and unselected users
      const selectedUserIds = new Set(this.entity.SelectedUsers?.map(u => u.Id) ?? []);
      
      const users: UserRow[] = [];

      // Add selected users first
      this.entity.SelectedUsers?.forEach(user => {
        users.push({
          Id: user.Id,
          RowVersion: user.RowVersion,
          UserId: user.UserId,
          UserName: user.UserName,
          SendingWayId: user.SendingWayId,
          selected: true
        });
      });

      // Add unselected users
      this.entity.UnSelectedUsers?.forEach(user => {
        if (!selectedUserIds.has(user.Id)) {
          users.push({
            UserId: user.Id,
            UserName: user.UserName,
            SendingWayId: 1, // Default to System
            selected: false
          });
        }
      });

      this.users.set(users);
    } else {
      // For add mode, show all users from getUserList
      this.users.set(this.allUsers.map(user => ({
        UserId: user.Id,
        UserName: user.UserName,
        SendingWayId: 1, // Default to System
        selected: false
      })));
    }

    this.initialUsersSnapshot = JSON.stringify(this.users());
  }

  private populateForm(): void {
    if (this.entity) {
      this.form.patchValue({
        notificationId: this.entity.Id,
      });

      // Add current notification to options if not present
      if (this.entity.Id && this.entity.Name) {
        const exists = this.notificationOptions.some(n => n.Id === this.entity!.Id);
        if (!exists) {
          this.notificationOptions = [{
            Id: this.entity.Id,
            Name: this.entity.Name,
            GeneralMessageEn: '',
            GeneralMessageAr: ''
          }, ...this.notificationOptions];
          this.syncNotificationSelectOptions();
        }
      }
    }
  }

  onNotificationSearch(term: string): void {
    this.notificationSearchInput$.next(term);
  }

  onUserSelectionChange(user: UserRow, checked: boolean): void {
    user.selected = checked;
    if (!checked) {
      user.SendingWayId = 1; // Reset to default when unchecked
    }
  }

  save(): void {
    // Validate form
    Object.keys(this.form.controls).forEach(key => {
      const control = this.form.get(key);
      control?.markAsTouched();
    });

    if (this.form.invalid) {
      this.feedback.error(
        'feedback.notificationSettings.selectionRequired.title',
        'feedback.notificationSettings.selectionRequired.message'
      );
      return;
    }

    const selectedUsers: SelectedNotificationUser[] = this.users()
      .filter(u => u.selected)
      .map(u => ({
        Id: u.Id,
        RowVersion: u.RowVersion,
        UserId: u.UserId,
        SendingWayId: u.SendingWayId
      }));

    const command = {
      ExistedNotificationId: this.mode === 'edit' && this.entity ? this.entity.Id : '',
      NewNotificationId: this.form.value.notificationId,
      SelectedNotificationUsers: selectedUsers
    };

    this.notificationUserService.updateNotificationUsers(command).subscribe({
      next: () => {
        this.feedback.success(
          'feedback.notificationSettings.usersUpdated.title',
          'feedback.notificationSettings.usersUpdated.message'
        );
        this.modal.close('success');
      },
      error: (err) => {
        this.feedback.handleApiError(err);
      }
    });
  }

saveAndNew(): void {
    // Validate form
    Object.keys(this.form.controls).forEach(key => {
      const control = this.form.get(key);
      control?.markAsTouched();
    });
    if (this.form.invalid) {
      this.feedback.error(
        'feedback.notificationSettings.selectionRequired.title',
        'feedback.notificationSettings.selectionRequired.message'
      );
      return;
    }
    const selectedUsers: SelectedNotificationUser[] = this.users()
      .filter(u => u.selected)
      .map(u => ({
        Id: u.Id,
        RowVersion: u.RowVersion,
        UserId: u.UserId,
        SendingWayId: u.SendingWayId
      }));
    const command = {
      ExistedNotificationId: this.mode === 'edit' && this.entity ? this.entity.Id : '',
      NewNotificationId: this.form.value.notificationId,
      SelectedNotificationUsers: selectedUsers
    };

    this.notificationUserService.updateNotificationUsers(command).subscribe({ 
      next: (result) => {
        this.feedback.success(
          'feedback.notificationSettings.usersUpdated.title',
          'feedback.notificationSettings.usersUpdated.message'
        );
        // Reset form for new entry
        this.form.reset();
        this.initializeUserRows();
        if (this.onSaveCallback) {
          this.onSaveCallback(result);
        }
      },
      error: (err) => {
        this.feedback.handleApiError(err);
      }
    });
  }

  cancel(): void {
    this.modal.close();
  }

  /** Consulted by ModalManagementService before closing the modal. Row
   * selection/sending-way edits live outside `form`, hence the snapshot
   * compare rather than a plain `form.dirty` check. */
  hasUnsavedChanges(): boolean {
    return this.form.dirty || JSON.stringify(this.users()) !== this.initialUsersSnapshot;
  }
}
