using ISTDocAi.Application.Common.Contracts.NotificationService;

namespace ISTDocAi.Application.Common.EmailTemplates;

public static class NotificationEmailTemplates
{
    public static string BuildNotificationEmail(ServerNotificationUserDetail payLoad)
    {
        return $@"<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; padding: 20px; background-color: #f5f5f5;'>
    <div style='max-width: 500px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 8px;'>
        <h2 style='color: #333; margin-top: 0;'>SkyPort Email Notification</h2>
        <p style='color: #666; line-height: 1.6;'>{payLoad.NotificationName}:</p>
        
        <div style='text-align: center; padding: 20px; background-color: #f8f9fa; border-radius: 6px; margin: 20px 0;'>
            <span style='font-size: 28px; font-weight: bold; color: #1890ff; letter-spacing: 5px;'>{payLoad.GeneralMessageEn}:{payLoad.CustomMessage}</span>
        </div>
         <div style='text-align: center; padding: 20px; background-color: #f8f9fa; border-radius: 6px; margin: 20px 0;'>
            <span style='font-size: 28px; font-weight: bold; color: #1890ff; letter-spacing: 5px;'>Sending At:{(payLoad.SendingDate.HasValue ? payLoad.SendingDate.Value.ToString("dd/MM/yyyy h:mm:ss") : "")}</span>
        </div>       
        <p style='color: #999; font-size: 12px; margin-top: 30px;'>If you didn't request this, please ignore this email.</p>
    </div>
</body>
</html>";
    }
}
