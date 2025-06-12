using System;
using System.Collections.Generic;
using Newtonsoft.Json;

//constants for property retrieval
// public const string TEXT_PROPERTY = "text";
// public const string TEXT_TYPE_PROPERTY = "textType";
// public const string DURATION_PROPERTY = "duration";
// public const string URL_PROPERTY = "url";

[Serializable]
public class ContentItem
{
    [JsonProperty("contentType")]
    public string contentType { get; set; } // e.g., "Text", "Image", "Video", "Timer"

    [JsonProperty("arObjectID")]
    public string arObjectID { get; set; } // Used to link to a specific ArObject

    [JsonIgnore]
    public ArObject arObject { get; set; } // Reference to the actual ArObject

    [JsonProperty("properties")]
    public Dictionary<string, object> properties { get; set; } = new Dictionary<string, object>();
}