import { Component, inject } from '@angular/core';

import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzModalRef, NZ_MODAL_DATA } from 'ng-zorro-antd/modal';

@Component({
  selector: 'app-notification-search',
  templateUrl: './notification-search.component.html',
  styleUrls: ['./notification-search.component.css'],
  imports: [
    FormsModule,
    ReactiveFormsModule,
    NzFormModule,
    NzInputModule,
    NzButtonModule
]
})
export class NotificationSearchComponent {
  private fb = inject(FormBuilder);
  private modal = inject(NzModalRef);
  data = inject(NZ_MODAL_DATA);

  form: FormGroup;

  constructor() {
    const data = this.data;

    const init = data ?? {};
    const filters = init.filters ?? init;

    this.form = this.fb.group({
      name: [filters?.name ?? ''],
      generalMessageEn: [filters?.generalMessageEn ?? ''],
    });
  }

  clear(): void {
    this.form.reset({
      name: '',
      generalMessageEn: '',
    });
  }

  save(): void {
    this.modal.close(this.form.value);
  }

  cancel(): void {
    this.modal.close();
  }
}
