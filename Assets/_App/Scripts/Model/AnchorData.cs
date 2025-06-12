using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Currently the only type of spatial anchored data are text notes
/// </summary>
[Serializable]
public class SpatialNoteAnchor
{
    public string id;

    public string text;
}

/// <summary>
/// 
/// </summary>
[Serializable]
public class AnchorData
{
    /// Version to help identify differences in file formats
    public int version;

    public List<SpatialNoteAnchor> anchors = new List<SpatialNoteAnchor>();
}