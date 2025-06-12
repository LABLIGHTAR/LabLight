using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[Serializable]
public class ProtocolDefinition
{
    [JsonProperty("version")]
    public string version { get; set; } // Version to help identify differences in file formats

    [JsonProperty("title")]
    public string title { get; set; } // Title of the protocol

    [JsonProperty("description")]
    public string description { get; set; } // Description of the protocol
    
    [JsonProperty("estimatedDurationInSeconds")]
    public int estimatedDurationInSeconds { get; set; }

    [JsonProperty("protocolPDFNames")]
    public List<string> protocolPDFNames { get; set; } = new List<string>(); // List of PDF file names

    [JsonProperty("globalArObjects")]
    public List<ArObject> globalArObjects { get; set; } = new List<ArObject>(); // List of global AR objects

    [JsonProperty("steps")]
    public List<StepDefinition> steps { get; set; } = new List<StepDefinition>(); // List of steps

    [JsonIgnore]
    public Dictionary<string, ArObject> arObjectLookup { get; private set; }

    public void BuildArObjectLookup()
    {
        arObjectLookup = new Dictionary<string, ArObject>();
        foreach (var arObject in globalArObjects)
        {
            if (!string.IsNullOrEmpty(arObject.arObjectID))
            {
                arObjectLookup[arObject.arObjectID] = arObject;
            }
        }
    }
    
    [JsonIgnore]
    public string mediaBasePath { get; set; }
}

