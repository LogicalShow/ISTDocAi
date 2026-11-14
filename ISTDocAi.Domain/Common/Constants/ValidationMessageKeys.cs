namespace ISTDocAi.Domain.Common.Constants;

public static class ValidationMessageKeys
{
    public const string AttachmentIdCannotBeEmpty = "Validation.AttachmentIdCannotBeEmpty";
    public const string AttachmentIdsRequired = "Validation.AttachmentIdsRequired";
    public const string DeleteItemsRequired = "Validation.DeleteItemsRequired";
    public const string FilePathRequired = "Validation.FilePathRequired";
    public const string FileSizeGreaterThanZero = "Validation.FileSizeGreaterThanZero";
    public const string IdRequired = "Validation.IdRequired";
    public const string ItemIdRequired = "Validation.ItemIdRequired";
    public const string ItemRowVersionRequired = "Validation.ItemRowVersionRequired";
    public const string OriginalFileNameRequired = "Validation.OriginalFileNameRequired";
    public const string ReferenceIdRequired = "Validation.ReferenceIdRequired";
    public const string ReferenceIdMaxLength = "Validation.ReferenceIdMaxLength";
    public const string ReferenceTypeRequired = "Validation.ReferenceTypeRequired";
    public const string RowVersionRequired = "Validation.RowVersionRequired";
    public const string SortFieldNotSupported = "Validation.SortFieldNotSupported";
    public const string SortFieldRequiredWhenSortOrderProvided = "Validation.SortFieldRequiredWhenSortOrderProvided";
    public const string SortOrderInvalid = "Validation.SortOrderInvalid";
    public const string StoredFileNameRequired = "Validation.StoredFileNameRequired";
    public const string ToDateOnOrAfterFromDate = "Validation.ToDateOnOrAfterFromDate";

    // IAM
    public const string NameEnRequired = "Validation.NameEnRequired";
    public const string NameArRequired = "Validation.NameArRequired";
    public const string UserNameRequired = "Validation.UserNameRequired";
    public const string EmailRequired = "Validation.EmailRequired";
    public const string EmailInvalid = "Validation.EmailInvalid";
    public const string PasswordRequired = "Validation.PasswordRequired";
    public const string NewPasswordRequired = "Validation.NewPasswordRequired";
    public const string OtpRequired = "Validation.OtpRequired";
}
