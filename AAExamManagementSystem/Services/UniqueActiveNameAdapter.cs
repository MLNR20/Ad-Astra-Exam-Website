using AAExamManagementSystem.Models.Validation;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;

namespace AAExamManagementSystem.Services;

/// <summary>Renders [UniqueActiveName] as data-val attributes so unique-name-validation.js checks the name in the browser.</summary>
public class UniqueActiveNameAttributeAdapter : AttributeAdapterBase<UniqueActiveNameAttribute>
{
    public const string CheckUrl = "/validation/unique-name";

    public UniqueActiveNameAttributeAdapter(UniqueActiveNameAttribute attribute, IStringLocalizer? localizer)
        : base(attribute, localizer)
    {
    }

    public override void AddValidation(ClientModelValidationContext context)
    {
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-uniquename", GetErrorMessage(context));
        MergeAttribute(context.Attributes, "data-val-uniquename-kind", Attribute.Kind.ToString());
        MergeAttribute(context.Attributes, "data-val-uniquename-url", CheckUrl);
    }

    public override string GetErrorMessage(ModelValidationContextBase validationContext) =>
        Attribute.FormatErrorMessage(string.Empty);
}

public class UniqueActiveNameAdapterProvider : IValidationAttributeAdapterProvider
{
    private readonly ValidationAttributeAdapterProvider _default = new();

    public IAttributeAdapter? GetAttributeAdapter(ValidationAttribute attribute, IStringLocalizer? stringLocalizer) =>
        attribute is UniqueActiveNameAttribute unique
            ? new UniqueActiveNameAttributeAdapter(unique, stringLocalizer)
            : _default.GetAttributeAdapter(attribute, stringLocalizer);
}
