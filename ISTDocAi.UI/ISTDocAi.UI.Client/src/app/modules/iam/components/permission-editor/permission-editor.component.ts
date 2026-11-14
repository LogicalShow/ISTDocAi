
import { Component, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonComponent } from '../../../../shared/components/button/button.component';
import { IconComponent } from '../../../../shared/icons/icon.component';
import { ApplicationModuleDto } from '../../models/permission-tree.model';
import { PermissionTreeComponent } from '../permission-tree/permission-tree.component';

@Component({
  selector: 'app-permission-editor',
  imports: [TranslatePipe, ButtonComponent, IconComponent, PermissionTreeComponent],
  templateUrl: './permission-editor.component.html',
  styleUrl: './permission-editor.component.css'
})
export class PermissionEditorComponent {
  readonly subjectType = input.required<string>();
  readonly subjectName = input.required<string>();
  readonly applications = input<ApplicationModuleDto[] | null>(null);
  readonly loading = input(false);
  readonly saving = input(false);
  readonly importing = input(false);
  readonly importAction = input<(() => void) | null>(null);
  readonly importDisabled = input(false);
  readonly importedSelection = input<readonly string[] | null>(null);

  readonly selectionChange = output<string[]>();
  readonly cancelRequested = output<void>();
  readonly saveRequested = output<void>();
}
