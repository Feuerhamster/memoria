using System.ComponentModel.DataAnnotations;
using Memoria.Attributes;

namespace Memoria.Models.Request;

public class ImageUploadRequest
{
    [Required]
    [MaxFileSize(1024 * 1024 * 6)] // 6 MB
    [AllowedMimeTypes(["image/jpeg", "image/avif", "image/png", "image/webp"])]
    public IFormFile Image { get; set; } = null!;
}