import { Component, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';

import { NzModalRef, NZ_MODAL_DATA } from 'ng-zorro-antd/modal';
import { TranslatePipe } from '@ngx-translate/core';
import { SaveableComponent } from '../../../../../core/models/modal-config.model';

@Component({
  selector: 'app-role-search',
  templateUrl: './role-search.component.html',
  styleUrl: './role-search.component.css',
  imports: [
    ReactiveFormsModule,
    TranslatePipe
]
})
export class RoleSearchComponent implements SaveableComponent {
  private fb = inject(FormBuilder);
  private modal = inject(NzModalRef);
  data = inject(NZ_MODAL_DATA);

  form: FormGroup;

  constructor() {
    const data = this.data;

    this.form = this.fb.group({
      name: [data?.name || '']
    });
  }

  save(): void {
    this.modal.close(this.form.value);
  }

  cancel(): void {
    this.modal.close();
  }
}




