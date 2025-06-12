using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class RotateModelButton : MonoBehaviour
{
    [SerializeField] private ArObjectViewController model;


    public void RotateModel(float degrees)
    {
        model.Rotate(degrees);
    }
}
