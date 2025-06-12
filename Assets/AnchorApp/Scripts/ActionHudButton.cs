using UnityEngine;
using PolySpatial.Samples;
using UnityEngine.Events;

// HubInputManager invoked press on HubButton
// Generic implementation of a button that can be pressed and invokes an event
public class ActionHudButton : HubButton
{
    public UnityEvent OnPress;

    public override void Press()
    {
        base.Press();

        OnPress?.Invoke();
    }
}

