import { HostInfo } from "./HostInfo.model";
import { UserInfo } from "./UserInfo.model";
import { SidebarItem } from "./sidebar-item.model";
import { UserMenuDto } from "./user-menu.model";

export interface AppStoreState {
  hostInfo: HostInfo;
  userInfo: UserInfo;
  connectionId: string | null;
  userMenuSource: UserMenuDto | null;
  localizedUserMenu: SidebarItem[] | null;
}
