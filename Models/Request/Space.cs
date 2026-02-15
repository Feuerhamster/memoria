using System.ComponentModel.DataAnnotations;

namespace Memoria.Models.Request;

public class SpaceCreateRequest
{
    [Required]
    [StringLength(64, MinimumLength = 4)]
    public string Name { get; set; }
    
    [StringLength(8000)]
    public string Description { get; set; }
}