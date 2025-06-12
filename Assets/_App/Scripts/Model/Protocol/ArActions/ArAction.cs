using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public class ArAction
{
    [JsonProperty("actionType")]
    public string actionType { get; set; } // e.g., "highlight", "timer"

    [JsonProperty("arObjectID")]
    public string arObjectID { get; set; } // Reference to the target ArObject

    [JsonIgnore]
    public ArObject arObject { get; set; } // Reference to the actual ArObject

    [JsonProperty("properties")]
    public Dictionary<string, object> properties { get; set; } = new Dictionary<string, object>();
}
