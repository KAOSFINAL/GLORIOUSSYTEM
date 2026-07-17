using System;
using System.Collections.Generic;

namespace GLORIOUSSYSTEM.Data.Models;

public partial class Pipe
{
    public int Id { get; set; }

    public int PipeNumber { get; set; }

    public string? Description { get; set; }

    public virtual ICollection<LeafClassification> LeafClassifications { get; set; } = new List<LeafClassification>();

    public virtual ICollection<Sensor> Sensors { get; set; } = new List<Sensor>();
}
