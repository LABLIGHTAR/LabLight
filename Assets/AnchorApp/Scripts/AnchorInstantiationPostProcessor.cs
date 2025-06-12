using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnchorInstantiationPostProcessor : MonoBehaviour
{
    public void ProcessGameObject(GameObject gameObject)
    {
        var controller = gameObject.GetComponent<AnchorController>();
        if (controller != null)
        {
            controller.DebugText = "Spawned";
        }
    }
}
