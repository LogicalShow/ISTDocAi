import { Injectable, inject } from '@angular/core';
import { SidebarItem } from '../models/sidebar-item.model';
import { AppStore } from '../state/app.store';
@Injectable({
  providedIn: 'root',
})
export class SidebarConfigService {
  private appStore = inject(AppStore);


  getSidebarItems(): SidebarItem[] {
    return this.appStore.getState().localizedUserMenu || [];
  }
}
