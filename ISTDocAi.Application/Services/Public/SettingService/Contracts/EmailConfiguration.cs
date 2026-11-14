namespace ISTDocAi.Application.Services.Public.SettingService.Contracts;

public class EmailConfiguration
{
    public const string MASKED_PASSWORD = "********";
    
    public bool IsEmailEnabled { get; set; }
    public string EmailHost { get; set; }
    public int EmailPort { get; set; }
    public string EmailUsername { get; set; }
    public string DecryptedPassword { get; set; }
    public string SenderEmail { get; set; }
    public bool EmailEnableSSL { get; set; }
}
