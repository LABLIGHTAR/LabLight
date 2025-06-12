// using Newtonsoft.Json;
// using Newtonsoft.Json.Converters;
// using Newtonsoft.Json.Linq;
// using System;

// /// <summary>
// /// Json conversion helper to create concrete ContentItem classes
// /// </summary>
// public class ContentItemConverter : CustomCreationConverter<ContentItem>
// {
//     ContentType _currentObjectType;

//     public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
//     {
//         var jobj = JObject.ReadFrom(reader);
//         _currentObjectType = jobj["contentType"].ToObject<ContentType>();
//         return base.ReadJson(jobj.CreateReader(), objectType, existingValue, serializer);
//     }
    
//     public override ContentItem Create(Type objectType)
//     {
//         switch (_currentObjectType)
//         {
//             case ContentType.Layout:
//                 return new LayoutItem();
//             case ContentType.Text:
//                 return new TextItem();
//             case ContentType.Image:
//                 return new ImageItem();
//             case ContentType.Video:
//                 return new VideoItem();
//             case ContentType.Sound:
//                 return new SoundItem();
//             case ContentType.Property:
//                 return new PropertyItem();
//             case ContentType.WebUrl:
//                 return new WebUrlItem();
//             default:
//                 throw new NotImplementedException();
//         }
//     }
// }