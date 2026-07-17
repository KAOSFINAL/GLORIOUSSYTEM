using System;
using System.Collections.Generic;

namespace GLORIOUSSYSTEM.Data.Models;

public partial class Actuator
{
    public int Id { get; set; }

    public int NodeId { get; set; }

    public string Name { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string? Pin { get; set; }

    public virtual ICollection<ActuatorEvent> ActuatorEvents { get; set; } = new List<ActuatorEvent>();

    public virtual Node Node { get; set; } = null!;
}
