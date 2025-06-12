// using System;
// using System.Globalization;
// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;
// using System.IO;
// using System.Data;

// public class convertCSVtoProtocol : MonoBehaviour
// {
//     private static List<Vector3> targetPositionsList = new List<Vector3>()
//     {
//         new Vector3(0,0,0),
//         new Vector3(-0.18f,0,0),
//         new Vector3(0,0,0.11f),
//         new Vector3(-0.18f,0,0.11f)
//     };
//     private static List<Vector3> sourcePositionsList = new List<Vector3>()
//     {
//         new Vector3(0.3314f, 0 ,0.12f),
//         new Vector3(0.3314f, 0, 0.06f),
//         new Vector3(0.3314f, 0, 0),
//         new Vector3(0.3314f, 0, -0.06f),
//         new Vector3(0.49f, 0, 0.12f),
//         new Vector3(0.49f, 0, 0.06f),
//         new Vector3(0.49f, 0, 0),
//         new Vector3(0.49f, 0, -0.06f)
//     };
//     private static Vector3 multiSourcePosition = new Vector3(0.3f, 0, 0.04f);

//     private static List<string> findChainIDs(string startID, string endID) //this function specifically takes in IDs and the number of ids possible
//     {
//         int numSelected = 0;
//         if(endID.Contains(';')) // ; followed by # selected indicates how many of the given ids you would like to select, in an even interval. Ex. A1-A8, numSelected = 4 -> chainIDs = {A1,A3,A5,A7}
//         {
//             string [] split = endID.Split(';');
//             endID = split[0];
//             numSelected = Int32.Parse(split[1]);
//         }
//         List<string> returnList = new List<string>();
//         if(Char.IsDigit(startID, 0)) //handles integer only chain ids
//         {
//             for(int i = Int32.Parse(startID); i <= Int32.Parse(endID); i++)
//             {
//                 returnList.Add(Convert.ToString(i));
//             }
//         }
//         else
//         {
//             if(startID[0] == endID[0]) //row chain id, ex. A1 - A12
//             {
//                 for(int i = Int32.Parse(startID.Substring(1)); i <= Int32.Parse(endID.Substring(1)); i++)
//                 {
//                     returnList.Add(startID.Substring(0,1) + Convert.ToString(i));
//                 }
//             }else //column chain id, ex. A1 - H1
//             {
//                 for(int i = (int)startID[0]; i <= (int)endID[0]; i++)
//                 {
//                     returnList.Add(Convert.ToString(Convert.ToChar(i)) + startID.Substring(1));
//                 }
//             }
//         }
//         if(numSelected > 0)
//         {
//             List<string> tempList = new List<string>();
//             for(int i = 0; i < returnList.Count(); i += ((returnList.Count() + 1)/numSelected)) //we want to spread out the number selected over the entire highlight group
//             {
//                 tempList.Add(returnList[i]);
//             }
//             returnList = tempList;
//         }
//         return returnList;
//     }

//     public static ProtocolDefinition ReadPipLightCSV(string [] lines, string name)
//     {
//         ProtocolDefinition protocol = new ProtocolDefinition();
//         protocol.version = 7;
//         protocol.title = name;

//         Debug.Log("Protocol '" + protocol.title + "' file version " + protocol.version);

//         protocol.steps = new List<StepDefinition>();

//         Queue<Vector3> sourcePositions = new Queue<Vector3>(sourcePositionsList);

//         Queue<Vector3> targetPositions = new Queue<Vector3>(targetPositionsList);

//         List<StepDefinition> returnList = new List<StepDefinition>();

//         StepDefinition currStep = null;

//         List<ArDefinition> bioMats = new List<ArDefinition>();

//         ArDefinition tempArDef;

//         ContainerArDefinition wellContainer = new ContainerArDefinition();

//         Dictionary<string, string> multiSourceTracking = new Dictionary<string, string>();//key = matid, value = contents

//         ImageItem wellPNG = new ImageItem()
//         {
//             url = "pipetHelper.png"
//         };
//         //wellContainer.Add(wellPNG);

//         foreach(string line in lines.Where(line => line != ""))
//         {
//             string [] fields = line.Split(',');
//             if(fields[0].Contains("material"))
//             {
//                 if(fields.Length > 5)
//                 {
//                     if(fields[3].Contains(":"))
//                     {
//                         if(protocol.globalArElements.Count() == Int32.Parse(fields[3].Substring(0, fields[3].IndexOf(':'))))
//                         {
//                             if(fields[1].Contains("tuberack")) //right now tuberack is only supported "multi" source -> fields[1].Contains("multi")...
//                             {
//                                 tempArDef = new ModelArDefinition
//                                 {
//                                     url = fields[1].Contains(":") ? fields[1].Substring(0, fields[1].IndexOf(':')) : fields[1] + ".prefab",
//                                     name = "Tube Rack",
//                                     position = multiSourcePosition,
//                                     rotation = Quaternion.identity,
//                                     condition = new AnchorCondition("Tube Rack")
//                                 };
//                             }
//                             else
//                             {
//                                 tempArDef = new ModelArDefinition
//                                 {
//                                     url = fields[1].Contains(":") ? fields[1].Substring(0, fields[1].IndexOf(':')) : fields[1] + ".prefab",
//                                     name = fields[1].Contains(":") ? fields[1].Substring(fields[1].IndexOf(':') + 1) : fields[1] + protocol.globalArElements.Count() + 1,
//                                     position = sourcePositions.Dequeue(),
//                                     rotation = Quaternion.identity,
//                                     condition = new AnchorCondition("Reservoir")
//                                 };
//                             }
//                             multiSourceTracking[fields[3]] = fields[4];
//                             protocol.globalArElements.Add(tempArDef);
//                         }else //append the contents to the model ar definition at that index
//                         {
//                             multiSourceTracking[fields[3]] = fields[4];
//                         }
//                         ModelArDefinition temp = (ModelArDefinition)protocol.globalArElements[Int32.Parse(fields[3].Substring(0, fields[3].IndexOf(':')))];//.contents.Add(fields[3].Substring(fields[3].IndexOf(':') + 1));
//                         temp.contentsToColors[fields[4]] = fields[5].Substring(0, fields[5].IndexOf(':')); //stores colorHex with contents
//                         protocol.globalArElements[Int32.Parse(fields[3].Substring(0, fields[3].IndexOf(':')))] = temp;
//                     }
//                 }
//                 else //target definition
//                 {
//                     tempArDef = new ModelArDefinition
//                     {
//                         url = fields[2] + (fields[1].Contains(":") ? fields[1].Substring(0, fields[1].IndexOf(':')) : fields[1] + ".prefab"),
//                         name = fields[1].Contains(":") ? fields[1].Substring(fields[1].IndexOf(':') + 1) : fields[1] + protocol.globalArElements.Count() + 1,
//                         position = targetPositions.Dequeue(),
//                         rotation = Quaternion.identity,
//                         condition = new AnchorCondition("96 Well Plate", (fields[1].Contains(":") ? fields[1].Substring(fields[1].IndexOf(':') + 1) : fields[1] + protocol.globalArElements.Count() + 1))
//                     };
//                     protocol.globalArElements.Add(tempArDef);
//                 }
//             }else if(fields[0].Contains("step"))
//             {
//                 if(protocol.steps.Count == 0)
//                 {
//                     StepDefinition lockingStep = new StepDefinition()
//                     {
//                         checklist = new List<CheckItemDefinition>()
//                     };
//                     CheckItemDefinition clearCheck = new CheckItemDefinition()
//                     {
//                         Text = "Clear your workbench"
//                     };
//                     lockingStep.checklist.Add(clearCheck);
//                     CheckItemDefinition check = new CheckItemDefinition()
//                     {
//                         Text = "Place requested items on workbench"
//                     };
//                     foreach(ArDefinition def in protocol.globalArElements)
//                     {
//                         ArOperation operation = new AnchorArOperation()
//                         {
//                             arDefinition = def
//                         };
//                         check.operations.Add(operation);
//                     }
//                     lockingStep.checklist.Add(check);
//                     protocol.steps.Add(lockingStep);
//                 }
//                 currStep = new StepDefinition()
//                 {
//                     checklist = new List<CheckItemDefinition>()
//                 };
//             }else if(fields[0].Contains("end"))
//             {
//                 if(currStep.checklist.Count() > 0)
//                     protocol.steps.Add(currStep);
//             }
//             else if(fields[0] == "")
//             {
//                 HighlightAction sourceHighlightAction = new HighlightAction()
//                 {
//                     actionName = fields[1].Substring(fields[1].IndexOf(':') + 1),
//                     isSource = true,
//                     chainIDs = fields[2].Contains(":") ? (fields[2].Contains("-") ? findChainIDs(fields[2].Substring(fields[2].IndexOf(':') + 1, (fields[2].IndexOf('-') - fields[2].IndexOf(':')) - 1), fields[2].Substring(fields[2].IndexOf('-') + 1)) : new List<string>(){fields[2].Substring(fields[2].IndexOf(':') + 1)}) : new List<string>(){fields[2]},
//                     colorInfo = new Tuple<string, string>(fields[3].Substring(0,fields[3].IndexOf(':')), fields[3].Substring(fields[3].IndexOf(':')+1)),
//                     contents = multiSourceTracking.ContainsKey(fields[2]) ? new Tuple<string, string>(multiSourceTracking[fields[2]].Substring(0,multiSourceTracking[fields[2]].IndexOf(':')), multiSourceTracking[fields[2]].Substring(multiSourceTracking[fields[2]].IndexOf(':') + 1)): new Tuple<string, string>("", ""),
//                     volume = new Tuple<float, string>(float.Parse(fields[4], CultureInfo.InvariantCulture), fields[5])
//                 };

//                 HighlightAction targetHighlightAction = new HighlightAction()
//                 {
//                     actionName = fields[1].Substring(fields[1].IndexOf(':') + 1),
//                     isSource = false,
//                     chainIDs = fields[6].Contains(":") ? (fields[6].Contains("-") ? findChainIDs(fields[6].Substring(fields[6].IndexOf(':')+1, (fields[6].IndexOf('-') - fields[6].IndexOf(':')) - 1), fields[6].Substring(fields[6].IndexOf('-') + 1)) : new List<string>(){fields[6].Substring(fields[6].IndexOf(':') + 1)}) : new List<string>(){fields[6]},
//                     colorInfo = new Tuple<string, string>(fields[7].Substring(0, fields[7].IndexOf(':')), fields[7].Substring(fields[7].IndexOf(':')+1)),
//                     contents = multiSourceTracking.ContainsKey(fields[2]) ? new Tuple<string, string>(multiSourceTracking[fields[2]].Substring(0, multiSourceTracking[fields[2]].IndexOf(':')), multiSourceTracking[fields[2]].Substring(multiSourceTracking[fields[2]].IndexOf(':') + 1)) : new Tuple<string, string>("", ""),
//                     volume = new Tuple<float, string>(float.Parse(fields[4], CultureInfo.InvariantCulture), fields[5])
//                 };
//                 //placeholder until better wording
//                 string checkText = sourceHighlightAction.actionName + " from " + (sourceHighlightAction.contents.Item1 != "" ? sourceHighlightAction.contents.Item1 : (sourceHighlightAction.chainIDs.Count() > 1 ? sourceHighlightAction.chainIDs[0] + "-" + sourceHighlightAction.chainIDs[sourceHighlightAction.chainIDs.Count()-1] : sourceHighlightAction.chainIDs[0]));
//                 checkText = checkText + " into " + (targetHighlightAction.chainIDs.Count() > 1 ? targetHighlightAction.chainIDs[0] + "-" + targetHighlightAction.chainIDs[targetHighlightAction.chainIDs.Count()-1] : targetHighlightAction.chainIDs[0]);

//                 CheckItemDefinition tempCheck = new CheckItemDefinition()
//                 {
//                     Text = checkText
//                 };

//                 int sourceParentID = fields[2].Contains(":") ? Int32.Parse(fields[2].Substring(0, fields[2].IndexOf(':'))) : Int32.Parse(fields[2]);
//                 int targetParentID = fields[6].Contains(":") ? Int32.Parse(fields[6].Substring(0, fields[6].IndexOf(':'))) : Int32.Parse(fields[6]);

//                 if(sourceParentID == targetParentID)
//                 {
//                     ArOperation operation = new HighlightArOperation()
//                     {
//                         HighlightName = sourceHighlightAction.highlightName,
//                         highlightActions = new List<HighlightAction>()
//                         {
//                             sourceHighlightAction,
//                             targetHighlightAction
//                         },
//                         arDefinition = protocol.globalArElements[sourceParentID]
//                     };
//                     tempCheck.operations.Add(operation);
//                 }else
//                 {
//                     ArOperation sourceOperation = new HighlightArOperation()
//                     {
//                         HighlightName = sourceHighlightAction.highlightName,
//                         highlightActions = new List<HighlightAction>()
//                         {
//                             sourceHighlightAction
//                         },
//                         arDefinition = protocol.globalArElements[sourceParentID]
//                     };
//                     ArOperation targetOperation = new HighlightArOperation()
//                     {
//                         HighlightName = targetHighlightAction.highlightName,
//                         highlightActions = new List<HighlightAction>()
//                         {
//                             targetHighlightAction
//                         },
//                         arDefinition = protocol.globalArElements[targetParentID]
//                     };
//                     tempCheck.operations.Add(sourceOperation);
//                     tempCheck.operations.Add(targetOperation);
//                 }
//                 currStep.checklist.Add(tempCheck);
//             }
//         }
//         return protocol;
//     }

//     public static List<StepDefinition> ReadStepsFromPipLightCSV(string [] lines)
//     {
//         Queue<Vector3> sourcePositions = new Queue<Vector3>(sourcePositionsList);

//         Queue<Vector3> targetPositions = new Queue<Vector3>(targetPositionsList);

//         List<StepDefinition> returnList = new List<StepDefinition>();

//         StepDefinition currStep = null;

//         List<ArDefinition> bioMats = new List<ArDefinition>();

//         ArDefinition tempArDef;

//         ContainerArDefinition wellContainer = new ContainerArDefinition();

//         Dictionary<string, string> multiSourceTracking = new Dictionary<string, string>();//key = matid, value = contents

//         ImageItem wellPNG = new ImageItem()
//         {
//             url = "pipetHelper.png"
//         };
//         //wellContainer.Add(wellPNG);

//         foreach(string line in lines.Where(line => line != ""))
//         {
//                 string [] fields = line.Split(',');
//                 if(fields[0].Contains("material"))
//                 {
//                     if(fields.Length > 5)
//                     {
//                         if(fields[3].Contains(":")) // we are dealing with a source that has multiple sub sources
//                         {
//                             if(bioMats.Count() == Int32.Parse(fields[3].Substring(0, fields[3].IndexOf(':'))))
//                             {
//                                 if(fields[1].Contains("tuberack")) //right now tuberack is only supported "multi" source -> fields[1].Contains("multi")...
//                                 {
//                                     tempArDef = new ModelArDefinition
//                                     {
//                                         url = fields[1].Contains(":") ? fields[1].Substring(0, fields[1].IndexOf(':')) : fields[1] + ".prefab",
//                                         name = fields[1].Contains(":") ? fields[1].Substring(fields[1].IndexOf(':') + 1) : Convert.ToString(bioMats.Count() + 1),
//                                         position = multiSourcePosition,
//                                         rotation = Quaternion.identity
//                                     };
//                                 }
//                                 else
//                                 {
//                                     tempArDef = new ModelArDefinition
//                                     {
//                                         url = fields[1].Contains(":") ? fields[1].Substring(0, fields[1].IndexOf(':')) : fields[1] + ".prefab",
//                                         name = fields[1].Contains(":") ? fields[1].Substring(fields[1].IndexOf(':') + 1) : fields[1] + bioMats.Count() + 1,
//                                         position = sourcePositions.Dequeue(),
//                                         rotation = Quaternion.identity
//                                         //condition = new TargetCondition("Reservoir") for testing locking
//                                     };
//                                 }
//                                 multiSourceTracking[fields[3]] = fields[4];
//                                 bioMats.Add(tempArDef);
//                             }else //append the contents to the model ar definition at that index
//                             {
//                                 multiSourceTracking[fields[3]] = fields[4];
//                             }
//                             ModelArDefinition temp = (ModelArDefinition)bioMats[Int32.Parse(fields[3].Substring(0, fields[3].IndexOf(':')))];//.contents.Add(fields[3].Substring(fields[3].IndexOf(':') + 1));
//                             temp.contentsToColors[fields[4]] = fields[5].Substring(0, fields[5].IndexOf(':')); //stores colorHex with contents
//                             bioMats[Int32.Parse(fields[3].Substring(0, fields[3].IndexOf(':')))] = temp;
//                         }
//                     }
//                     else //target definition
//                     {
//                         tempArDef = new ModelArDefinition
//                         {
//                             url = fields[2] + (fields[1].Contains(":") ? fields[1].Substring(0, fields[1].IndexOf(':')) : fields[1] + ".prefab"),
//                             name = fields[1].Contains(":") ? fields[1].Substring(fields[1].IndexOf(':') + 1) : fields[1] + bioMats.Count() + 1,
//                             position = targetPositions.Dequeue(),
//                             rotation = Quaternion.identity
//                             //condition = new TargetCondition("96 Well Plate") for testing locking
//                         };
//                         bioMats.Add(tempArDef);
//                     }
//                 }
//                 else if(fields[0].Contains("step"))
//                 {
//                     /*
//                     //this is a temporary initial alignment step to be removed when alignment controller is complete AM 4/11/2023
//                     if(currStep == null) //if this is the first step that we are creating, add alignment steps
//                     {
//                         StepDefinition alignmentStep = new StepDefinition();
//                         foreach(ModelArDefinition def in bioMats)
//                         {
//                             ModelArDefinition alignmentDef = new ModelArDefinition()
//                             {
//                                 url = def.url,
//                                 position = def.position,
//                                 rotation = Quaternion.identity,
//                                 target = "",
//                                 highlight = "alignment"
//                             };
//                             alignmentStep.arElements.Add(alignmentDef);
//                         }
//                         alignmentStep.arElements.Add(wellContainer);
//                         returnList.Add(alignmentStep);
//                     }*/

//                     currStep = new StepDefinition()
//                     {
//                         checklist = new List<CheckItemDefinition>()
//                     };
//                 }
//                 else if(fields[0].Contains("end"))
//                 {
//                     if(currStep.checklist.Count() > 0)
//                         returnList.Add(currStep);
//                 }
//                 else if(fields[0] == "")
//                 {
//                     HighlightAction sourceHighlightAction = new HighlightAction()
//                     {
//                         actionName = fields[1].Substring(fields[1].IndexOf(':') + 1),
//                         isSource = true,
//                         chainIDs = fields[2].Contains(":") ? (fields[2].Contains("-") ? findChainIDs(fields[2].Substring(fields[2].IndexOf(':') + 1, (fields[2].IndexOf('-') - fields[2].IndexOf(':')) - 1), fields[2].Substring(fields[2].IndexOf('-') + 1)) : new List<string>(){fields[2].Substring(fields[2].IndexOf(':') + 1)}) : new List<string>(){fields[2]},
//                         colorInfo = new Tuple<string, string>(fields[3].Substring(0,fields[3].IndexOf(':')), fields[3].Substring(fields[3].IndexOf(':')+1)),
//                         contents = multiSourceTracking.ContainsKey(fields[2]) ? new Tuple<string, string>(multiSourceTracking[fields[2]].Substring(0,multiSourceTracking[fields[2]].IndexOf(':')), multiSourceTracking[fields[2]].Substring(multiSourceTracking[fields[2]].IndexOf(':') + 1)): new Tuple<string, string>("", ""),
//                         volume = new Tuple<float, string>(float.Parse(fields[4], CultureInfo.InvariantCulture), fields[5])
//                     };

//                     HighlightAction targetHighlightAction = new HighlightAction()
//                     {
//                         actionName = fields[1].Substring(fields[1].IndexOf(':') + 1),
//                         isSource = false,
//                         chainIDs = fields[6].Contains(":") ? (fields[6].Contains("-") ? findChainIDs(fields[6].Substring(fields[6].IndexOf(':')+1, (fields[6].IndexOf('-') - fields[6].IndexOf(':')) - 1), fields[6].Substring(fields[6].IndexOf('-') + 1)) : new List<string>(){fields[6].Substring(fields[6].IndexOf(':') + 1)}) : new List<string>(){fields[6]},
//                         colorInfo = new Tuple<string, string>(fields[7].Substring(0, fields[7].IndexOf(':')), fields[7].Substring(fields[7].IndexOf(':')+1)),
//                         contents = multiSourceTracking.ContainsKey(fields[2]) ? new Tuple<string, string>(multiSourceTracking[fields[2]].Substring(0, multiSourceTracking[fields[2]].IndexOf(':')), multiSourceTracking[fields[2]].Substring(multiSourceTracking[fields[2]].IndexOf(':') + 1)) : new Tuple<string, string>("", ""),
//                         volume = new Tuple<float, string>(float.Parse(fields[4], CultureInfo.InvariantCulture), fields[5])
//                     };
//                     //placeholder until better wording
//                     string checkText = sourceHighlightAction.actionName + " from " + (sourceHighlightAction.contents.Item1 != "" ? sourceHighlightAction.contents.Item1 : (sourceHighlightAction.chainIDs.Count() > 1 ? sourceHighlightAction.chainIDs[0] + "-" + sourceHighlightAction.chainIDs[sourceHighlightAction.chainIDs.Count()-1] : sourceHighlightAction.chainIDs[0]));
//                     checkText = checkText + " into " + (targetHighlightAction.chainIDs.Count() > 1 ? targetHighlightAction.chainIDs[0] + "-" + targetHighlightAction.chainIDs[targetHighlightAction.chainIDs.Count()-1] : targetHighlightAction.chainIDs[0]);

//                     CheckItemDefinition tempCheck = new CheckItemDefinition()
//                     {
//                         Text = checkText
//                     };

//                     int sourceParentID = fields[2].Contains(":") ? Int32.Parse(fields[2].Substring(0, fields[2].IndexOf(':'))) : Int32.Parse(fields[2]);
//                     int targetParentID = fields[6].Contains(":") ? Int32.Parse(fields[6].Substring(0, fields[6].IndexOf(':'))) : Int32.Parse(fields[6]);

//                     if(sourceParentID == targetParentID)
//                     {
//                         ArOperation operation = new HighlightArOperation()
//                         {
//                             HighlightName = sourceHighlightAction.highlightName,
//                             highlightActions = new List<HighlightAction>()
//                             {
//                                 sourceHighlightAction,
//                                 targetHighlightAction
//                             },
//                             arDefinition = bioMats[sourceParentID]
//                         };
//                         tempCheck.operations.Add(operation);
//                     }else
//                     {
//                         ArOperation sourceOperation = new HighlightArOperation()
//                         {
//                             HighlightName = sourceHighlightAction.highlightName,
//                             highlightActions = new List<HighlightAction>()
//                             {
//                                 sourceHighlightAction
//                             },
//                             arDefinition = bioMats[sourceParentID]
//                         };
//                         ArOperation targetOperation = new HighlightArOperation()
//                         {
//                             HighlightName = targetHighlightAction.highlightName,
//                             highlightActions = new List<HighlightAction>()
//                             {
//                                 targetHighlightAction
//                             },
//                             arDefinition = bioMats[targetParentID]
//                         };
//                         tempCheck.operations.Add(sourceOperation);
//                         tempCheck.operations.Add(targetOperation);
//                     }
//                     currStep.checklist.Add(tempCheck);
//                 }
//         }
//         return returnList;
//     }

//     public static ProtocolDefinition ReadPoolingCSV(string[] lines, string name)
//     {
//         ProtocolDefinition protocol = new ProtocolDefinition();
//         protocol.version = 7;
//         protocol.title = name;

//         Debug.Log("Protocol '" + protocol.title + "' file version " + protocol.version);

//         protocol.steps = new List<StepDefinition>();

//         Queue<Vector3> sourcePositions = new Queue<Vector3>(sourcePositionsList);

//         Queue<Vector3> targetPositions = new Queue<Vector3>(targetPositionsList);

//         List<StepDefinition> returnList = new List<StepDefinition>();

//         List<ArDefinition> bioMats = new List<ArDefinition>();

//         ArDefinition tempArDef;

//         ContainerArDefinition wellContainer = new ContainerArDefinition();

//         DataTable poolingTable = new DataTable();
//         poolingTable.Columns.Add("sampleName", typeof(string));
//         poolingTable.Columns.Add("wellID", typeof(int));
//         poolingTable.Columns.Add("poolNum", typeof(int));
//         poolingTable.Columns.Add("volume", typeof(float));

//         bool firstLine = true;
//         foreach (string line in lines.Where(line => line != ""))
//         {
//             string[] fields = line.Split(',');  /* [0]SampleName, [1]PlateWell#, [2]CTs, [3]LibraryQubit, [4]Pool#, [5]uLInPool, [6]ngInPool */

//             //skip first line
//             if (firstLine)
//             {
//                 firstLine = false;
//                 continue;
//             }

//             poolingTable.Rows.Add(fields[0], int.Parse(fields[1]), int.Parse(fields[4]), float.Parse(fields[5]));
//         }

//         //add extraction plates
//         int numExtractionPlates = (int)Math.Ceiling((int)(poolingTable.Compute("max([wellID])", string.Empty)) / 96.0);
//         for(int i=0; i<numExtractionPlates; i++)
//         {
//             tempArDef = new ModelArDefinition
//             {
//                 url = "horizontalwellplate96.prefab",
//                 name = "extraction plate " + (i + 1).ToString(),
//                 position = targetPositions.Dequeue(),
//                 rotation = Quaternion.identity,
//                 condition = new AnchorCondition("96 Well Plate", "extraction plate " + (i + 1).ToString())
//             };
//             protocol.globalArElements.Add(tempArDef);
//         }

//         //add target plates
//         int numPoolingPlates = (int)Math.Ceiling((int)(poolingTable.Compute("max([poolNum])", string.Empty)) / 96.0);
//         for (int i = 0; i < numExtractionPlates; i++)
//         {
//             tempArDef = new ModelArDefinition
//             {
//                 url = "horizontalwellplate96.prefab",
//                 name = "pooling plate " + (i + 1).ToString(),
//                 position = targetPositions.Dequeue(),
//                 rotation = Quaternion.identity,
//                 condition = new AnchorCondition("96 Well Plate", "pooling plate " + (i + 1).ToString())
//             };
//             protocol.globalArElements.Add(tempArDef);
//         }

//         //add locking step
//         StepDefinition lockingStep = new StepDefinition()
//         {
//             checklist = new List<CheckItemDefinition>()
//         };
//         CheckItemDefinition clearCheck = new CheckItemDefinition()
//         {
//             Text = "Clear your workbench"
//         };
//         lockingStep.checklist.Add(clearCheck);
//         CheckItemDefinition check = new CheckItemDefinition()
//         {
//             Text = "Place requested items on workbench"
//         };
//         foreach (ArDefinition def in protocol.globalArElements)
//         {
//             ArOperation operation = new AnchorArOperation()
//             {
//                 arDefinition = def
//             };
//             check.operations.Add(operation);
//         }
//         lockingStep.checklist.Add(check);
//         protocol.steps.Add(lockingStep);


//         StepDefinition poolingStep = new StepDefinition()
//         {
//             checklist = new List<CheckItemDefinition>()
//         };
//         //add pooling step
//         foreach (DataRow row in poolingTable.Rows)
//         {
//             HighlightAction sourceHighlightAction = new HighlightAction()
//             {
//                 actionName = "transfer",
//                 isSource = true,
//                 chainIDs = new List<string>() { GetWellID((int)row["wellID"])},
//                 colorInfo = new Tuple<string, string>("#FF0000", "Red"),
//                 contents = new Tuple<string, string>("",""),
//                 volume = new Tuple<float, string>((float)row["volume"], " �L")
//             };

//             HighlightAction targetHighlightAction = new HighlightAction()
//             {
//                 actionName = "transfer",
//                 isSource = false,
//                 chainIDs = new List<string>() { GetWellID((int)row["poolNum"]) },
//                 colorInfo = new Tuple<string, string>("#00FF00", "Green"),
//                 contents = new Tuple<string, string>("", ""),
//                 volume = new Tuple<float, string>((float)row["volume"], " �L")
//             };
//             //placeholder until better wording
//             string checkText = "Transfer " + row["sampleName"] + " from well " + sourceHighlightAction.chainIDs[0] + " into well " + targetHighlightAction.chainIDs[0] + " (pool # " + row["poolNum"] + ")";

//             CheckItemDefinition tempCheck = new CheckItemDefinition()
//             {
//                 Text = checkText
//             };

//             int sourceParentID = (int)Math.Ceiling((int)(row["wellID"]) / 96.0) - 1;
//             int targetParentID = numExtractionPlates + (int)Math.Ceiling((int)(row["poolNum"]) / 96.0) - 1;

//             if (sourceParentID == targetParentID)
//             {
//                 ArOperation operation = new HighlightArOperation()
//                 {
//                     HighlightName = sourceHighlightAction.highlightName,
//                     highlightActions = new List<HighlightAction>()
//                         {
//                             sourceHighlightAction,
//                             targetHighlightAction
//                         },
//                     arDefinition = protocol.globalArElements[sourceParentID]
//                 };
//                 tempCheck.operations.Add(operation);
//             }
//             else
//             {
//                 ArOperation sourceOperation = new HighlightArOperation()
//                 {
//                     HighlightName = sourceHighlightAction.highlightName,
//                     highlightActions = new List<HighlightAction>()
//                         {
//                             sourceHighlightAction
//                         },
//                     arDefinition = protocol.globalArElements[sourceParentID]
//                 };
//                 ArOperation targetOperation = new HighlightArOperation()
//                 {
//                     HighlightName = targetHighlightAction.highlightName,
//                     highlightActions = new List<HighlightAction>()
//                         {
//                             targetHighlightAction
//                         },
//                     arDefinition = protocol.globalArElements[targetParentID]
//                 };
//                 tempCheck.operations.Add(sourceOperation);
//                 tempCheck.operations.Add(targetOperation);
//             }
//             poolingStep.checklist.Add(tempCheck);
//         }
//         protocol.steps.Add(poolingStep);

//         return protocol;
//     }

//     private static string GetWellID(int wellNum)
//     {
//         if(wellNum > 96)
//         {
//             wellNum -= 96;
//         }
//         string wellId = "ABCDEFGH"[(wellNum - 1) % 8] + ((int)Math.Floor((wellNum - 1) / 8.0) + 1).ToString();
//         //Debug.Log("well Num: " + wellNum);
//         //Debug.Log("well ID: " + wellId);
//         return wellId;
//     }
// }
