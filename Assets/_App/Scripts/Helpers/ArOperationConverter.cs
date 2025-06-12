// using Newtonsoft.Json;
// using Newtonsoft.Json.Converters;
// using Newtonsoft.Json.Linq;
// using System;

// /// <summary>
// /// Json conversion helper to create concrete ArOperation classes
// /// </summary>
// public class ArOperationConverter : CustomCreationConverter<ArOperation>
// {
//     ArOperationType _currentObjectType;

//     public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
//     {
//         var jobj = JObject.ReadFrom(reader);

//         _currentObjectType = jobj["arOperationType"].ToObject<ArOperationType>();
 
//         return base.ReadJson(jobj.CreateReader(), objectType, existingValue, serializer);
//     }
    
//     public override ArOperation Create(Type objectType)
//     {
//         switch (_currentObjectType)
//         {
//             case ArOperationType.Highlight:
//                 return new HighlightArOperation();
//             //case ArOperationType.Animation:
//             //    return new AnimationArOperation();
//             case ArOperationType.Anchor:
//                 return new AnchorArOperation();
//             default:
//                 throw new NotImplementedException();
//         }
//     }
// }