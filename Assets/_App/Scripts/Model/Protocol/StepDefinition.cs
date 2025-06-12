
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;


[Serializable]
public class StepDefinition
{
    [JsonProperty("isCritical")]
    public bool isCritical { get; set; }
    [JsonProperty("title")]
    public string title { get; set; }

    [JsonProperty("estimatedDurationInSeconds")]
    public int estimatedDurationInSeconds { get; set; }

    [JsonProperty("contentItems")]
    public List<ContentItem> contentItems { get; set; } = new List<ContentItem>();

    [JsonProperty("checklist")]
    public List<CheckItemDefinition> checklist { get; set; } = new List<CheckItemDefinition>();
}