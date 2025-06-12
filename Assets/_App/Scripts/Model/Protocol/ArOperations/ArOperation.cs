// using Newtonsoft.Json;
// using UnityEngine;

// public enum ArOperationType
// {
//     Highlight = 0,
//     Anchor = 1
//     //Animation = 2
// }

// /// <summary>
// /// Abstract operation that describes an operation that needs to be performed an arView
// /// </summary>
// public abstract class ArOperation
// {
//     [HideInInspector]
//     public ArOperationType arOperationType;

//     [JsonProperty(IsReference = true)]
//     //[DisableInNonPrefabs]
//     public ArDefinition arDefinition;

//     public virtual string ListElementLabelName()
//     {
//         return "Operation";
//     }

//     public abstract void Apply(ArElementViewController arView);
// }

