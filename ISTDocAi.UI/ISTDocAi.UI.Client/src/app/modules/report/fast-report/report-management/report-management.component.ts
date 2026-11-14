import { Component } from '@angular/core';
import { ReportViewerComponent } from "../report-viewer/report-viewer.component";

@Component({
  selector: 'app-report-management',
  imports: [ReportViewerComponent],
  templateUrl: './report-management.component.html',
  styles: ``
})
export class ReportManagementComponent {
selectedInvoiceId = '12345'; // Example invoice ID
}
