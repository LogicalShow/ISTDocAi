export interface ServerNotification {
  Id: string;
  Type: string;
  Message: string;
  Time: Date;
  IsRead: boolean;
}

export interface ServerNotificationUserDetail {
  NotificationId: string;
  NotificationName: string;
  GeneralMessageEn?: string | null;
  GeneralMessageAr?: string | null;
  TargetUrl?: string | null;
  TargetId?: string | null;
  CustomMessage?: string | null;
  SendingDate?: string | null;
  ActionDate?: string | null;
  ReceivedNotificationStatus?: number | null;
  CorrelationId: string ;
}
