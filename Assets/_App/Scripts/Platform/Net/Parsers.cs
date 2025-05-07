using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

/// <summary>
/// Static helper methods to convert parsed JSON data into instanced classes
/// </summary>
public class Parsers
{
    // public static JsonSerializerSettings oldSerializerSettings = new JsonSerializerSettings
    // {
    //     PreserveReferencesHandling = PreserveReferencesHandling.Objects,
    //     ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
    //     NullValueHandling = NullValueHandling.Ignore,
    //     Converters = { new ArDefinitionConverter(),
    //                    new ArOperationConverter(),
    //                    new ContentItemConverter(),}
    // };

    public static JsonSerializerSettings serializerSettings = new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters = new List<JsonConverter>
        {
            new PropertiesConverter()
        }
    };

    public static List<ProtocolDefinition> ParseProtocolList(List<string> jsonStrings)
    {
        try
        {
            var protocolDefinitions = new List<ProtocolDefinition>();
            
            foreach (var jsonString in jsonStrings)
            {
                protocolDefinitions.Add(ParseProtocol(jsonString));
            }

            return protocolDefinitions;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing protocols: {e.Message}");
            throw;
        }
    }

    public static ProtocolDefinition ParseProtocol(string jsonString)
    {
        try
        {
            var protocolDefinition = new ProtocolDefinition();
            protocolDefinition = JsonConvert.DeserializeObject<ProtocolDefinition>(jsonString, serializerSettings);
            if (protocolDefinition == null)
            {
                throw new Exception("Failed to parse protocol - result was null");
            }
            // Validate required fields
            if (string.IsNullOrEmpty(protocolDefinition.title))
            {
                throw new Exception("Protocol definition missing required Title field");
            }
            if (string.IsNullOrEmpty(protocolDefinition.description))
            {
                throw new Exception("Protocol definition missing required Description field"); 
            }
            if (string.IsNullOrEmpty(protocolDefinition.version))
            {
                throw new Exception("Protocol definition missing required Version field");
            }

            // Build lookup dictionary for AR objects
            protocolDefinition.BuildArObjectLookup();

            // Link AR objects to their references in content items and actions
            LinkArObjects(protocolDefinition);

            return protocolDefinition;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing protocol: {e.Message}");
            throw;
        }
    }

    private static void LinkArObjects(ProtocolDefinition protocol)
    {
        // Link AR objects in steps
        foreach (var step in protocol.steps)
        {
            // Link content items
            foreach (var contentItem in step.contentItems)
            {
                if (!string.IsNullOrEmpty(contentItem.arObjectID) && 
                    protocol.arObjectLookup.TryGetValue(contentItem.arObjectID, out var arObject))
                {
                    contentItem.arObject = arObject;
                }
            }

            // Link checklist items
            foreach (var checkItem in step.checklist)
            {
                // Link content items in checklist
                foreach (var contentItem in checkItem.contentItems)
                {
                    if (!string.IsNullOrEmpty(contentItem.arObjectID) && 
                        protocol.arObjectLookup.TryGetValue(contentItem.arObjectID, out var arObject))
                    {
                        contentItem.arObject = arObject;
                    }
                }

                // Link AR actions
                foreach (var arAction in checkItem.arActions)
                {
                    // Handle Lock action type with empty arIDList
                    if(arAction.actionType == "lock" && arAction.properties.ContainsKey("arIDList"))
                    {
                        var tempList = arAction.properties["arIDList"] as List<object>;
                        if(tempList != null)
                        {
                            var stringList = tempList.Select(x => x?.ToString()).ToList();
                            if(stringList.Count == 0)
                            {
                                arAction.properties["arIDList"] = protocol.arObjectLookup.Keys.ToList();
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(arAction.arObjectID) && 
                        protocol.arObjectLookup.TryGetValue(arAction.arObjectID, out var arObject))
                    {
                        arAction.arObject = arObject;
                    }
                }
            }
        }
    }

    public static WorkspaceFrame ParseWorkspace(string json)
    {
        // Workspace values are in meters
        try
        {
            var workspace = new WorkspaceFrame();

            var root = JObject.Parse(json);
            workspace.cameraPosition = vec3((JArray)root["camera"]);
            workspace.border = new List<Vector2>();

            var points = (JArray)root["border"];
            foreach (JArray pt in points.Children())
            {
                workspace.border.Add(vec2(pt));
            }

            return workspace;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Parsing anchor data: " + e.ToString());
            throw;
        }
    }

    public static NetStateFrame ParseNetStateFrame(string json)
    {
        try
        {
            var frame = new NetStateFrame();

            var root = JObject.Parse(json);
            frame.master = (string)root["master"];
            frame.procedure = (string)root["procedure"];
            frame.step = (int)root["step"];

            var screen = (JObject)root["screen"];
            var screenPos = (JArray)screen["pos"];
            var screenVec = (JArray)screen["vec"];

            frame.screen = new PositionRotation()
            {
                position = vec3(screenPos),
                lookForward = vec3(screenVec),
            };

            frame.objects = new List<TrackedObject>();

            var objects = (JArray)root["objects"];
            foreach (JObject obj in objects.Children())
            {

                var center = (JArray)obj["center"];
                var size = (JArray)obj["size"];
                var angle = (float)obj["angle"];
                var z = (float)obj["z"];

                frame.objects.Add(new TrackedObject()
                {
                    id = (int)obj["id"],
                    label = (string)obj["label"],
                    angle = angle,
                    scale = new Vector3((float)size[0], z, (float)size[1]),
                    position = new Vector3((float)center[0], 0, (float)center[1]),
                    rotation = Quaternion.AngleAxis(angle, Vector3.up)
                });
            }

            return frame;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Parsing protocol index: " + e.ToString());
            throw;
        }
    }

    // public static ProtocolDefinition ParseProtocol(string json)
    // {
    //     try
    //     {
    //         var protocol = new ProtocolDefinition();

    //         var root = JObject.Parse(json);
    //         protocol.version = (root["version"] == null) ? 0 : (int)root["version"];

    //         protocol.title = (string)root["title"];

    //         Debug.Log("Protocol '" + protocol.title + "' file version " + protocol.version);

    //         if (protocol.version >= 1)
    //         {
    //             protocol = JsonConvert.DeserializeObject<ProtocolDefinition>(json, serializerSettings);
    //         }
    //         else
    //         { 
    //             Debug.LogError("Version 0 procedure is no longer supported");
    //         }


    //         return protocol;
    //     }
    //     catch (System.Exception e)
    //     {
    //         Debug.LogError("Parsing protocol index: " + e.ToString());
    //         throw;
    //     }
    // }

    static Vector3 vec3(JArray arr)
    {
        return new Vector3((float)arr[0], (float)arr[1], (float)arr[2]);
    }

    static Vector2 vec2(JArray arr)
    {
        return new Vector2((float)arr[0], (float)arr[1]);
    }

    // public static ProtocolDefinition ConvertWellPlateCsvToProtocol(string filename, string csvString) depricated
    // {
    //     try
    //     {
    //         ProtocolDefinition protocol = new ProtocolDefinition();
    //         protocol.version = 7;
    //         protocol.title = filename;

    //         Debug.Log("Protocol '" + protocol.title + "' file version " + protocol.version);

    //         protocol.steps = new List<StepDefinition>();
    //         //protocol.steps = convertCSVtoProtocol.ReadStepsFromCSV(csvString.Split('\n'));

    //         if(filename.Contains("piplight_"))
    //         {
    //             //protocol = convertCSVtoProtocol.ReadPipLightCSV(csvString.Split('\n'), filename);
    //         }
    //         else if(filename.Contains("pooling_"))
    //         {
    //             //protocol = convertCSVtoProtocol.ReadPoolingCSV(csvString.Split('\n'), filename);
    //         }

    //         return protocol;
    //     }
    //     catch (System.Exception e)
    //     {
    //         Debug.LogError("Parsing protocol index: " + e.ToString());
    //         throw;
    //     }
    // }

    public static AnchorData ParseAnchorData(string json)
    {
        try
        {
            var anchorData = new AnchorData();

            var root = JObject.Parse(json);
            anchorData.version = (root["version"] == null) ? 0 : (int)root["version"];

            if (anchorData.version >= 1)
            {
                anchorData = JsonConvert.DeserializeObject<AnchorData>(json, serializerSettings);
            }
            else
            {
                Debug.LogError("Anchor data is missing version.");
            }

            return anchorData;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Parsing anchor data: " + e.ToString());
            throw;
        }
    }
} 