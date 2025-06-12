using UnityEngine;

public enum ConditionType
{
    Target = 0,   // Show when id detected 
    Every = 1,    // Instantiate a visualization for each detection
    Align = 2,    // Show when multiple detections align properly 
    Zone = 3,     // Show when in zone
    Anchor = 4,   // Lock position to detection position
}

/// <summary>
/// Conditions determine when a certain ar visualization will be shown
/// </summary>
public class Condition
{
    [HideInInspector]
    public ConditionType conditionType;

    public virtual bool IsSpecific()
    {
        return true;
    }

    public virtual bool IsTargeted()
    {
        return false;
    }

    public virtual string[] Targets()
    {
        return null;
    }
}
