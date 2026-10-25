using System.ComponentModel.DataAnnotations;

namespace ProductCatalog.Api.Validation;

/// <summary>Rejects the all-zero GUID after HTTP model binding.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute() : base("The product ID must be a nonempty GUID.") { }

    public override bool IsValid(object? value) => value is Guid id && id != Guid.Empty;
}
