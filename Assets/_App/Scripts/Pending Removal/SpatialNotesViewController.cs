using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class SpatialNotesViewController : LLBasePanel
{
    public void OnEnable()
    {
        SessionState.SpatialNoteEditMode.Value = true;
    }

    public void OnDisable()
    {
        SessionState.SpatialNoteEditMode.Value = false;
    }

    public void CloseSpatialNotesMenu()
    {
        SceneLoader.Instance.LoadSceneClean("ProtocolMenu");
    }
}
