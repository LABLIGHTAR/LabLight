using System;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using Newtonsoft.Json;

[Serializable]
public class CheckItemDefinition
{
    [JsonProperty("Text")]
    public string Text { get; set; }

    [JsonProperty("contentItems")]
    public List<ContentItem> contentItems { get; set; } = new List<ContentItem>();

    [JsonProperty("arActions")]
    public List<ArAction> arActions { get; set; } = new List<ArAction>();
}