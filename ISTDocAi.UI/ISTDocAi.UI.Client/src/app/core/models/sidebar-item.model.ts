import { IconName } from '../../shared/icons/app-icons';

export interface SidebarChildItem {
  label: string;
  url?: string;
  children?: SidebarChildItem[];
  openInNewTab?: boolean;
}

export interface SidebarItem {
  title: string;
  icon: IconName;
  url?: string;
  openInNewTab?: boolean;
  children: SidebarChildItem[];
}
