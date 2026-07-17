using System;
using System.Collections.Generic;

namespace GLORIOUSSYSTEM.Data.Models;

public partial class Camera
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Angle { get; set; }

    public string? Model { get; set; }

    public virtual ICollection<LeafClassification> LeafClassifications { get; set; } = new List<LeafClassification>();
}
