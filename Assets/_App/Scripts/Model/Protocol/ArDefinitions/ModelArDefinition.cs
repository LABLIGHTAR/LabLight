// using System;
// using System.Collections.Generic;
// using UnityEngine;

// /// <summary>
// /// 3D Model positioned in the Charuco frame
// /// </summary>
// public class ModelArDefinition : ArDefinition
// {
//     public string url;
    
//     public Vector3 position;

//     public Quaternion rotation;

//     public string name;

//     //public List<string> contents = new List<string>();

//     public Dictionary<string, string> contentsToColors = new Dictionary<string, string>();

//     // Retrieval playing of animations are not supported yet
//     public string animation;

//     // Activate single highlightgroup by name
//     public string highlight;

//     public ModelArDefinition()
//     {
//         arDefinitionType = ArDefinitionType.Model;
//     }

//     public override string ToString()
//     {
//         return url;
//     }
//     /*
//     [Button("Create Highlight Operation in New Checklist Item")]
//     private void CreateHighlightOperationNewCheckItem()
//     {
//         var hl = new HighlightArOperation();
//         hl.arDefinition = this;
//         ProcedureExplorer.AddOperationInCheckItem(hl);
//     }*/

//     //private void CreateHighlightOperationCurrentCheckItem()
//     //{
//     //    var hl = new HighlightArOperation();
//     //    hl.arDefinition = this;
//     //    ProcedureExplorer.AddOperationToCheckItem(hl);
//     //}
//     ///*
//     //[Button("Create Anchor Operation in New Checklist Item")]
//     //private void CreateAnchorOperationNewCheckItem()
//     //{
//     //    var al = new AnchorArOperation();
//     //    al.arDefinition = this;
//     //    ProcedureExplorer.AddOperationInCheckItem(al);
//     //}*/

//     //[Button("Create Anchor Operation in Current Checklist Item")]
//     //private void CreateAnchorOperationCurrentCheckItem()
//     //{
//     //    var al = new AnchorArOperation();
//     //    al.arDefinition = this;
//     //    ProcedureExplorer.AddOperationToCheckItem(al);
//     //}

//     public override string ListElementLabelName()
//     {
//         return "Model AR Definition";
//     }
// }
