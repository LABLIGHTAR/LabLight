// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// /// <summary>
// /// Operation to highlight a certain part in an ArView. 
// /// </summary>
// public class HighlightArOperation : ArOperation
// {
//     /// <summary>
//     /// Part to select/activate
//     /// </summary>
//     public string HighlightName;

//     public List<HighlightAction> highlightActions;

//     public HighlightArOperation()
//     {
//        arOperationType = ArOperationType.Highlight;
//     }

//     public override void Apply(ArElementViewController arView)
//     {
//        ((ModelElementViewController)arView).HighlightGroup(highlightActions);
//     }

//     public override string ListElementLabelName()
//     {
//         return "Highlight Operation";
//     }
// }

