// Query interfaces
export interface NotificationGetListQuery {
  Name?: string;
  GeneralMessageEn?: string;
  GeneralMessageAr?: string;
  GeneralSearch?: boolean;
  SearchText?: string;
  PageNo?: number;
  PageRowCount?: number;
  SortBy?: string;
  SortDirection?: string;
}

// Response interfaces
export interface NotificationListItem {
  Id: string;
  Name: string;
  GeneralMessageEn?: string;
  GeneralMessageAr?: string;
}

export interface NotificationUser {
  Id: string;
  RowVersion: string;
  UserId: string;
  UserName: string;
  SendingWayId: number; // Enum value: System=1, Email=2, Both=3
  SendingWayName: string;
}

export interface NotificationUserLookup {
  Id: string;
  UserName: string;
}

export interface NotificationDetails {
  Id: string;
  Name: string;
  SelectedUsers: NotificationUser[];
  UnSelectedUsers: NotificationUserLookup[];
}

export interface NotificationLookupItem {
  Id: string;
  Name: string;
  GeneralMessageEn: string;
  GeneralMessageAr: string;
}
