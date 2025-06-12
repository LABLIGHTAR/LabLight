using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ArObject
{
    // [JsonIgnore] 
    // public Condition placementCondition; AM TODO -> rework condition to get condition from prefab (view)

    [JsonProperty("specificObjectName")]
    public string specificObjectName { get; set; }


    [JsonProperty("arObjectID")]
    public string arObjectID { get; set; }

    [JsonProperty("rootPrefabName")]
    public string rootPrefabName { get; set; }
}   