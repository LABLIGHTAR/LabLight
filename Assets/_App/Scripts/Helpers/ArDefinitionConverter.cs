// using Newtonsoft.Json;
// using Newtonsoft.Json.Converters;
// using Newtonsoft.Json.Linq;
// using System;
// using UnityEngine;

// /// <summary>
// /// Json conversion helper to create concrete ArDefinition classes
// /// </summary>
// public class ArDefinitionConverter : CustomCreationConverter<ArDefinition>
// {
//     const string refProperty = "$ref";
//     const string arDefinitionTypeProperty = "arDefinitionType";

//     // Deprecated property name, use arDefinitionTypeProperty
//     const string actionProperty = "action";

//     ArDefinitionType _currentObjectType;

//     public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
//     {
//         var jobj = JObject.ReadFrom(reader);

//         var refId = (string)jobj[refProperty].RemoveFromLowestPossibleParent();
//         if (refId != null)
//         {
//             var reference = serializer.ReferenceResolver.ResolveReference(serializer, refId);
//             if (reference != null)
//                 return reference;
//         }

//         if (jobj[arDefinitionTypeProperty] != null)
//         {
//             _currentObjectType = jobj[arDefinitionTypeProperty].ToObject<ArDefinitionType>();
//         }
//         else
//         {
//             _currentObjectType = jobj["action"].ToObject<ArDefinitionType>();
//         }

//         return base.ReadJson(jobj.CreateReader(), objectType, existingValue, serializer);
//     }
    
//     public override ArDefinition Create(Type objectType)
//     {
//         switch (_currentObjectType)
//         {
//             case ArDefinitionType.Line:
//                 return new LineArDefinition();
//             case ArDefinitionType.Model:
//                 return new ModelArDefinition();
//             case ArDefinitionType.Outline:
//                 return new OutlineArDefinition();
//             case ArDefinitionType.Overlay:
//                 return new OverlayArDefinition();
//             case ArDefinitionType.Container:
//                 return new ContainerArDefinition();
//             case ArDefinitionType.Mask:
//                 return new MaskArDefinition();
//             case ArDefinitionType.Arrow:
//                 return new ArrowArDefinition();
//             case ArDefinitionType.BoundingBox:
//                 return new BoundingBoxArDefinition();
//             default:
//                 throw new NotImplementedException();
//         }
//     }
// }