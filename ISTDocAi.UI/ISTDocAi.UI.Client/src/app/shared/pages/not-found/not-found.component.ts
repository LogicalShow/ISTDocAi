import { Component } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { ButtonComponent } from '../../components/button/button.component';

@Component({
  selector: 'app-not-found',
  imports: [RouterModule, ButtonComponent, TranslatePipe],
  templateUrl: './not-found.component.html',
  styleUrls: ['./not-found.component.css']
})
export class NotFoundComponent {}
