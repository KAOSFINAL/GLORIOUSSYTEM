using System;
using System.Collections.Generic;

namespace GLORIOUSSYSTEM.Data.Models;

public partial class Node
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<Actuator> Actuators { get; set; } = new List<Actuator>();

    public virtual ICollection<Display> Displays { get; set; } = new List<Display>();

    public virtual ICollection<Sensor> Sensors { get; set; } = new List<Sensor>();
}
