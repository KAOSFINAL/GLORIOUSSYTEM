using System;

namespace GLORIOUSSYSTEM.Data.Models;

public partial class Display
{
    public int Id { get; set; }

    public int NodeId { get; set; }

    public string Name { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string? Model { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public int TouchEnabled { get; set; }

    public virtual Node Node { get; set; } = null!;
}