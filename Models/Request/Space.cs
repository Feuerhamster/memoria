using System.ComponentModel.DataAnnotations;
using Memoria.Models.Database;

namespace Memoria.Models.Request;

public class SpaceCreateRequest
{
    [Required]
    [StringLength(64, MinimumLength = 4)]
    public string Name { get; set; }
    
    [StringLength(8000)]
    public string Description { get; set; }
}

public class SpaceUpdateRequest : IDataUpdateObject<Space>
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    
    public Guid? ImageId { get; set; }
    public bool DeleteImage { get; set; }
    
    public RessourceAccessPolicy? AccessPolicy { get; set; }
    public bool? AllowJoins { get; set; }
    public Guid? OwnerUserId { get; set; }

    public void Apply(Space space)
    {
        if (this.Name != null)
        {
            space.Name = this.Name;
        }

        if (this.Description != null)
        {
            space.Description = this.Description;
        }

        if (this.Color != null)
        {
            space.Color = this.Color;
        }

        if (this.ImageId != null)
        {
            space.ImageId = this.ImageId;
        } else if (this.DeleteImage)
        {
            space.ImageId = null;
        }

        if (this.AccessPolicy != null)
        {
            space.AccessPolicy = this.AccessPolicy.Value;
        }

        if (this.AllowJoins != null)
        {
            space.AllowJoins = this.AllowJoins.Value;
        }

        if (this.OwnerUserId != null)
        {
            this.OwnerUserId = space.OwnerUserId;
        }
    }
}