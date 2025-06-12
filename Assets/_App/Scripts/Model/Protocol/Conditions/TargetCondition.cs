

/// <summary>
/// Condition to show a visualization when the given target id is detected (previously embedded in ArDefinition by default
/// </summary>
public class TargetCondition : Condition
{
    /// <summary>
    /// Currently label of the object to attach to
    /// TODO allow multiple targets so a single visualization can be controlled by multiple specific trackedobjects
    /// </summary>
    public string target = "";

    public TargetCondition()
    {
        conditionType = ConditionType.Target;
    }

    public TargetCondition(string target)
    {
        conditionType = ConditionType.Target;
        this.target = target;
    }

    public override bool IsTargeted()
    {
        return true;
    }

    public override string[] Targets()
    {
        return new string[]{ target };
    }
}
