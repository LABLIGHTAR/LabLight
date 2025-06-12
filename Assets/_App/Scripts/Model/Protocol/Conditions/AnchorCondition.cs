using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnchorCondition : Condition
{
    /// <summary>
    /// Label filter
    /// </summary>
    public string filter = "";

    public string target = "";

    public AnchorCondition()
    {
        conditionType = ConditionType.Anchor;
    }

    public AnchorCondition(string filter)
    {
        conditionType = ConditionType.Anchor;
        this.filter = filter;
    }

    public AnchorCondition(string target, string filter)
    {
        conditionType = ConditionType.Anchor;
        this.target = target;
        this.filter = filter;
    }

    public override bool IsSpecific()
    {
        return true;
    }

    public override bool IsTargeted()
    {
        return !string.IsNullOrEmpty(target);
    }

    public override string[] Targets()
    {
        return new string[] { target };
    }
}
