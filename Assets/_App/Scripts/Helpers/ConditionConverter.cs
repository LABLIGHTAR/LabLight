// using Newtonsoft.Json;
// using Newtonsoft.Json.Converters;
// using Newtonsoft.Json.Linq;
// using System;

// /// <summary>
// /// Json conversion helper to create concrete Condition classes
// /// </summary>
// public class ConditionConverter : CustomCreationConverter<Condition>
// {
//     const string conditionTypeProperty = "conditionType";

//     ConditionType _currentObjectType;

//     public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
//     {
//         var jobj = JObject.ReadFrom(reader);

//         if (jobj[conditionTypeProperty] == null)
//             return null;
        
//         _currentObjectType = jobj[conditionTypeProperty].ToObject<ConditionType>();

//         return base.ReadJson(jobj.CreateReader(), objectType, existingValue, serializer);
//     }
    
//     public override Condition Create(Type objectType)
//     {
//         switch (_currentObjectType)
//         {
//             case ConditionType.Target:
//                 return new TargetCondition();
//             case ConditionType.Every:
//                 return new EveryCondition();
//             case ConditionType.Anchor:
//                 return new AnchorCondition();
//             default:
//                 throw new NotImplementedException();
//         }
//     }
// }