using System;

namespace KN.KloudIdentity.Mapper.Domain.Application;

public class Action
{
    public int Id { get; set; }

    public string AppId { get; set; } = null!;

    public ActionNames ActionName { get; set; }

    public ActionTargets ActionTarget { get; set; }

    public virtual ICollection<ActionStep> ActionSteps { get; set; } = new List<ActionStep>();
}
