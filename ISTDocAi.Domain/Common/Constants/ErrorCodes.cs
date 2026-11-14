namespace ISTDocAi.Domain.Common.Constants;

public static class ErrorCodes
{
    public const string AggregateError = "AggregateError";
    public const string ErrorNullValue = "Error.NullValue";
    public const string ValidationError = "ValidationError";
    public const string ValidationFailed = "ValidationFailed";
    public const string Concurrency = "Concurrency";
    public const string UniqueConstraintViolation = "UniqueConstraintViolation";
    public const string DateRangeOverlap = "DateRangeOverlap";
    public const string Unexpected = "Unexpected";
    public const string RateLimited = "RateLimited";
    public const string AttachmentAddInvalid = "Attachment.Add.Invalid";
    public const string AttachmentDeleteInvalid = "Attachment.Delete.Invalid";
    public const string AttachmentFileError = "Attachment.File.Error";
    public const string AttachmentFileNotFound = "Attachment.File.NotFound";
    public const string AttachmentNotFound = "Attachment.NotFound";
    public const string AuthInactiveUser = "Auth.InactiveUser";
    public const string AuthInvalidCredentials = "Auth.InvalidCredentials";
    public const string AuthUnauthorized = "Auth.Unauthorized";
    public const string AuthForbidden = "Auth.Forbidden";
    public const string AuthUserIdentityMissing = "Auth.UserIdentityMissing";
    public const string IdempotencyKeyRequired = "Idempotency.Key.Required";
    public const string DateRangeContinuityBroken = "DateRange.ContinuityBroken";
    public const string DateRangeInvalidDuration = "DateRange.InvalidDuration";
    public const string DateRangeOverlapValidation = "DateRange.Overlap";
    public const string EmailDecryptionFailed = "Email.DecryptionFailed";
    public const string EmailDisabled = "Email.Disabled";
    public const string EmailIncomplete = "Email.Incomplete";
    public const string EmailPartialSendFailed = "Email.PartialSendFailed";
    public const string EmailSendFailed = "Email.SendFailed";
    public const string EmailValidationFailed = "Email.ValidationFailed";
    public const string NotificationNoUserAssigned = "Notification.NoUserAssigned";
    public const string NotificationNotFound = "Notification.NotFound";
    public const string NotificationUserUpsertFailed = "NotificationUser.UpsertFailed";
    public const string OtpInvalid = "Otp.Invalid";
    public const string PasswordChangeFailed = "Password.ChangeFailed";
    public const string PasswordResetFailed = "Password.ResetFailed";
    public const string PermissionInvalidPageActions = "Permission.InvalidPageActions";
    public const string RoleConflict = "Role.Conflict";
    public const string RoleDuplicateName = "Role.DuplicateName";
    public const string RoleNotFound = "Role.NotFound";
    public const string RoleRequired = "Role.Required";
    public const string SettingFooterConflict = "Setting.FooterConflict";
    public const string SettingHeaderConflict = "Setting.HeaderConflict";
    public const string SettingImageNotFound = "Setting.Image.NotFound";
    public const string SettingInvalid = "Setting.Invalid";
    public const string SettingInvalidImageType = "Setting.InvalidImageType";
    public const string SettingLogoConflict = "Setting.LogoConflict";
    public const string SettingNotFound = "Setting.NotFound";
    public const string UserAdminNotFound = "User.AdminNotFound";
    public const string UserConflict = "User.Conflict";
    public const string UserDuplicateEmail = "User.DuplicateEmail";
    public const string UserEmailNotFound = "User.EmailNotFound";
    public const string UserNotFound = "User.NotFound";
    public const string UserRequired = "User.Required";

    private const string UserErrorPrefix = "User.";
    private const string RoleErrorPrefix = "Role.";

    public static string UserIdentity(string identityErrorCode) => $"{UserErrorPrefix}{identityErrorCode}";
    public static string RoleIdentity(string identityErrorCode) => $"{RoleErrorPrefix}{identityErrorCode}";
}
