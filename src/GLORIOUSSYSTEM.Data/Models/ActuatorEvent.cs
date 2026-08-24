using System;
using System.Collections.Generic;

namespace GLORIOUSSYSTEM.Data.Models;

public partial class ActuatorEvent
{
    public int Id { get; set; }

    public int ActuatorId { get; set; }

    public DateTime Timestamp { get; set; }

    public string Action { get; set; } = null!;

    public string? Reason { get; set; }

    public virtual Actuator Actuator { get; set; } = null!;
}
