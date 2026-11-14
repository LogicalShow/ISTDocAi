// Command interfaces
export interface SelectedNotificationUser {
  Id?: string;
  RowVersion?: string;
  UserId: string;
  SendingWayId: number; // Enum value: System=1, Email=2, Both=3
}

export interface NotificationUserUpsertCommand {
  ExistedNotificationId: string;
  NewNotificationId: string;
  SelectedNotificationUsers: SelectedNotificationUser[];
}

// Response interfaces
export interface ReferenceHit {
  DependentEntity: string;
  Table: string;
  ForeignKey: string;
  Count: number;
}

export interface BlockedDeleteItem {
  Id: string;
  NameEn?: string;
  NameAr?: string;
  References: ReferenceHit[];
}

export interface NotificationUserUpsertCommandResponse {
  Message: string;
  DeletedIds: string[];
  Blocked: BlockedDeleteItem[];
}

export interface UserGetListQueryResponse{
    Id: string;
    UserName: string;
}

export interface ServerNotificationUserDetail {
  NotificationId: string;
  NotificationName: string;
  GeneralMessageEn?: string;
  GeneralMessageAr?: string;
  TargetUrl?: string;
  TargetId?: string;
  CustomMessage?: string;
  SendingDate?: string;
  ActionDate?: string;
  ReceivedNotificationStatus?: number;
  CorrelationId: string;
}