import { Component, inject, input, output } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { NzMenuModule } from 'ng-zorro-antd/menu';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { TranslationService } from '../../../core/services/translation.service';
import { Culture } from '../../../core/constants/culture.constants';
import { IconComponent } from '../../../shared/icons/icon.component';
import { SidebarItem } from '../../../core/models/sidebar-item.model';

@Component({
  selector: 'app-sidebar',
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.css',
  imports: [
    NgTemplateOutlet,
    RouterLink,
    NzMenuModule,
    NzTooltipModule,
    IconComponent
  ],
})
export class SidebarComponent {
  private router = inject(Router);
  private translationService = inject(TranslationService);

  readonly sidebarItems = input<SidebarItem[]>([]);
  readonly isMobile = input(false);
  readonly sidebarIsCollapsed = input(true);

  /** Emitted when a menu link is clicked, so the layout can scroll to top and close the mobile drawer. */
  readonly navigate = output<void>();

  currentCulture(): Culture {
    return this.translationService.culture();
  }

  openInNewTab(url: string): void {

    const urlWithToken = this.router.serializeUrl(
      this.router.createUrlTree([url])
    );

    window.open(urlWithToken, '_blank', 'noopener,noreferrer');
  }
}
