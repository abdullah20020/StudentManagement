using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Localization;

namespace StudentManagement.Localization
{
    /// <summary>Lets MVC's data-annotation localization (labels, validation messages) use <see cref="L"/>.</summary>
    public class UiStringLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, L.T(name));

        public LocalizedString this[string name, params object[] arguments] => new(name, L.T(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Enumerable.Empty<LocalizedString>();
    }

    /// <summary>
    /// Gives every property a translatable display name and every validation attribute a translatable
    /// message (the framework's built-in English messages aren't localizable otherwise).
    /// </summary>
    public class LocalizedMetadataProvider : IDisplayMetadataProvider, IValidationMetadataProvider
    {
        public void CreateDisplayMetadata(DisplayMetadataProviderContext context)
        {
            if (context.Key.MetadataKind != ModelMetadataKind.Property) return;

            var english = context.Attributes.OfType<DisplayAttribute>().FirstOrDefault()?.Name
                          ?? context.Attributes.OfType<DisplayNameAttribute>().FirstOrDefault()?.DisplayName
                          ?? Humanize(context.Key.Name!);
            context.DisplayMetadata.DisplayName = () => L.T(english);
        }

        public void CreateValidationMetadata(ValidationMetadataProviderContext context)
        {
            foreach (var attr in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
            {
                if (attr.ErrorMessage != null || attr.ErrorMessageResourceName != null) continue;
                attr.ErrorMessage = attr switch
                {
                    RequiredAttribute => "The {0} field is required.",
                    RangeAttribute => "{0} must be between {1} and {2}.",
                    EmailAddressAttribute => "{0} isn't a valid email address.",
                    PhoneAttribute => "{0} isn't a valid phone number.",
                    MaxLengthAttribute => "{0} can be at most {1} characters.",
                    StringLengthAttribute => "{0} can be at most {1} characters.",
                    _ => null,
                };
            }
        }

        /// <summary>"DateOfBirth" → "Date Of Birth".</summary>
        private static string Humanize(string name) => Regex.Replace(name, "(?<=[a-z])(?=[A-Z])", " ");

        /// <summary>Translatable versions of the model-binding error messages.</summary>
        public static void LocalizeBindingMessages(MvcOptions options)
        {
            var m = options.ModelBindingMessageProvider;
            m.SetAttemptedValueIsInvalidAccessor((value, field) => L.T("'{0}' isn't a valid value for {1}.", value, field));
            m.SetMissingBindRequiredValueAccessor(field => L.T("A value for {0} wasn't provided.", field));
            m.SetMissingKeyOrValueAccessor(() => L.T("A value is required."));
            m.SetMissingRequestBodyRequiredValueAccessor(() => L.T("A value is required."));
            m.SetNonPropertyAttemptedValueIsInvalidAccessor(value => L.T("'{0}' isn't a valid value.", value));
            m.SetNonPropertyUnknownValueIsInvalidAccessor(() => L.T("The value isn't valid."));
            m.SetUnknownValueIsInvalidAccessor(field => L.T("The value isn't valid for {0}.", field));
            m.SetValueIsInvalidAccessor(value => L.T("'{0}' isn't a valid value.", value));
            m.SetValueMustBeANumberAccessor(field => L.T("{0} must be a number.", field));
            m.SetNonPropertyValueMustBeANumberAccessor(() => L.T("This field must be a number."));
            m.SetValueMustNotBeNullAccessor(value => L.T("The {0} field is required.", value));
        }
    }
}
