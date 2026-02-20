using System.ComponentModel.DataAnnotations;

namespace Memoria.Attributes;


public class MaxFileSizeAttribute(long maxFileSize) : ValidationAttribute {
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not IFormFile file) {
            return ValidationResult.Success;
        }

        if (file.Length > maxFileSize)
        {
            return new ValidationResult(ErrorMessage ?? $"Maximum allowed file size is {maxFileSize / (1024*1024)} MB.");
        }
		
        return ValidationResult.Success;
    }
}

public class AllowedMimeTypesAttribute(string[] mimeTypes) : ValidationAttribute {
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not IFormFile file) {
            return ValidationResult.Success;
        }

        if (!mimeTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            return new ValidationResult(ErrorMessage ?? $"Only the following MIME types are allowed: {string.Join(", ", mimeTypes)}.");
        }
        return ValidationResult.Success;
    }
}