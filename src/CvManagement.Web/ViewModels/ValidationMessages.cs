namespace CvManagement.Web.ViewModels;

/// <summary>
/// Resource keys for DataAnnotations error messages. With DataAnnotations localization pointed at
/// SharedResource (Program.cs), an attribute's ErrorMessage is looked up as a key in
/// SharedResource.{culture}.resx -- so the same attribute produces English or Russian messages, both
/// server-side and in the client-side (unobtrusive) validation it emits. Placeholders follow
/// DataAnnotations: {0} = the (localized) display name, then the attribute's own arguments.
/// </summary>
public static class ValidationMessages
{
    public const string Required = "Validation_Required";
    public const string StringLength = "Validation_StringLength";
    public const string StringLengthRange = "Validation_StringLengthRange";
    public const string Range = "Validation_Range";
    public const string Email = "Validation_Email";
    public const string PasswordsMatch = "Validation_PasswordsMatch";
}
