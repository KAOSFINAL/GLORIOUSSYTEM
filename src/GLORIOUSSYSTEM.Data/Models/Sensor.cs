using System;
using System.Collections.Generic;

namespace GLORIOUSSYSTEM.Data.Models;

public partial class Sensor
{
    public int Id { get; set; }

    public int NodeId { get; set; }

    public string Name { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string? Model { get; set; }

    public int? PipeId { get; set; }

    public int? PositionIndex { get; set; }

    public string? Notes { get; set; }

    public int Enabled { get; set; }

    public double? MinThreshold { get; set; }

    public double? MaxThreshold { get; set; }

    public virtual Node Node { get; set; } = null!;

    public virtual Pipe? Pipe { get; set; }

    public virtual ICollection<Reading> Readings { get; set; } = new List<Reading>();
}
