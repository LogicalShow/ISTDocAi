import { Component, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';

import { NzModalRef, NZ_MODAL_DATA } from 'ng-zorro-antd/modal';
import { TranslatePipe } from '@ngx-translate/core';
import { SaveableComponent } from '../../../../../core/models/modal-config.model';

@Component({
    selector: 'app-user-search',
      templateUrl: './user-search.component.html',
    styleUrl: './user-search.component.css',
      imports: [
    ReactiveFormsModule,
    TranslatePipe
]
})
export class UserSearchComponent implements SaveableComponent {
    private fb = inject(FormBuilder);
    private modal = inject(NzModalRef);
    data = inject(NZ_MODAL_DATA);

    form: FormGroup;

    constructor() {
        const data = this.data;

        this.form = this.fb.group({
            userName: [data?.userName || ''],
            email: [data?.email || '']
        });
    }

    save(): void {
        this.modal.close(this.form.value);
    }

    cancel(): void {
        this.modal.close();
    }
}
