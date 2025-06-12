// using System;
// using System.Text;
// using System.Collections;
// using System.Collections.Generic;
// using System.IO;
// using UnityEngine;
// using System.Linq;
// using UniRx;
// using TMPro;


// //TODO
// //variable to verify if object is currently active in scene

// [RequireComponent(typeof(CapsuleCollider))]
// public class WellPlateViewController : ModelElementViewController
// {
//     public SettingsManagerScriptableObject settingsManagerSO;
//     public bool debugEnableAllSettings = false;
    
//     public bool modelActive;

//     [SerializeField]
//     public HighlightGroup wellPlateAlignmentGroup;

//     public bool isSource; //if object only acts as a source
//     //if source -> fill in Sources & nametag objects only

//     [SerializeField] private Transform Model;

//     [SerializeField]
//     public Transform Markers;

//     public Transform Markers2D;

//     public Transform rowIndicators;
//     public Transform rowHighlights;

//     public Transform colIndicators;
//     public Transform colHighlights;

//     [SerializeField]
//     public Transform Plate2D;

//     [SerializeField]
//     public Transform infoPanel;

//     [SerializeField]
//     public Color defaultIndicatorColor;

//     public Transform Cover;

//     public Transform Outline;

//     public List<ArAction> currActions;

//     private bool disableComponents = false;

//     private bool alignmentTriggered;

//     private int prevCheckItem = 0;

//     private List<LablightSettings> wellPlateSettings = new List<LablightSettings>
//     {
//         LablightSettings.RC_Markers,
//         LablightSettings.Relevant_RC_Only,
//         LablightSettings.RC_Highlights,
//         LablightSettings.Wellplate_Info_Panel,
//         LablightSettings.Well_Indicators
//     };

//     private Dictionary<LablightSettings, bool> storedSettings = new Dictionary<LablightSettings, bool>();

//     // New property to track active highlights
//     private List<ArAction> activeHighlights = new List<ArAction>();

//     void Awake()
//     {
//         ProtocolState.Instance.StepStream.Subscribe(Step => OnStepChanged(Step)).AddTo(this);
//         ProtocolState.Instance.ChecklistStream.Subscribe(_ => OnCheckItemChanged()).AddTo(this);
//     }

//     public override void Initialize(ArObject arObject, List<TrackedObject> trackedObjects)
//     {
//         base.Initialize(arObject, trackedObjects);

//         if(ModelName != null)
//         {
//             ModelName.GetComponent<TextMeshProUGUI>().text = arObject.specificObjectName;
//             ModelName.gameObject.SetActive(true);
//         }

//         foreach(LablightSettings setting in wellPlateSettings)
//         {
//             storedSettings.Add(setting, settingsManagerSO.GetSettingValue(setting));
//         }
//         AddSubscriptions();
//     }

//     private void InitializeMarkers2D()
//     {
//         bool firstHighlight = true;
//         if (Markers2D != null && arObject.specificObjectName.Contains("extraction"))
//         {
//             toggleTransform(Plate2D, true);
            
//             // Deactivate all markers
//             foreach (Transform marker in Markers2D)
//             {
//                 marker.gameObject.SetActive(false);
//             }

//             // Activate markers for wells to be highlighted
//             foreach (var checkItem in ProtocolState.Instance.CurrentChecklist)
//             {
//                 foreach (var action in checkItem.arActions.Where(op => op.actionType == "highlight"))
//                 {
//                     if (action?.properties != null)
//                     {
//                         bool isSource = (bool)action.properties.GetValueOrDefault("isSource", false);
//                         var subIDs = GetSubIDs(action);

//                         if (isSource && Markers2D != null && subIDs != null)
//                         {
//                             foreach (string id in subIDs)
//                             {
//                                 if (firstHighlight)
//                                 {
//                                     toggleTransform(Markers2D, true, id, Color.green);
//                                     firstHighlight = false;
//                                 }
//                                 else
//                                 {
//                                     toggleTransform(Markers2D, true, id, Color.blue);
//                                 }
//                             }
//                         }
//                     }
//                 }
//             }
//         }
//     }

//     public override void AlignmentGroup()
//     {
//         if(!disableComponents)
//         {
//             alignmentTriggered = true;
//             wellPlateAlignmentGroup.SubParts.ForEach(subPart => subPart.SetActive(true));
//         }
//     }
//     //resets model back to previous highlight if there is one
//     public override void ResetToCurrentHighlights()
//     {
//         if(!disableComponents)
//         {
//             alignmentTriggered = false;
//             //toggleTransform(ModelName, false);
//             wellPlateAlignmentGroup.SubParts.ForEach(subPart => subPart.SetActive(false));

//             if(currActions != null && modelActive)
//             {
//                 HighlightGroup(currActions);
//             }
//         }
//     }
//             //new imp
//     public override void HighlightGroup(List<ArAction> actions)
//     {
//         if (actions == null || actions.Count == 0 || alignmentTriggered)
//             return;

//         // Reset disableComponents when new highlights are enabled
//         disableComponents = false;

//         // Disable previous highlights first
//         disablePreviousHighlight();

//         modelActive = true;
//         activeHighlights = actions;
//         currActions = actions;  // Ensure currActions is updated

//         foreach (var action in actions)
//         {
//             EnableHighlight(action);
//         }

//         if (debugEnableAllSettings)
//         {
//             toggleIndicators(true);
//         }
//         else
//         {
//             toggleIndicators(storedSettings[LablightSettings.RC_Markers]);
//         }

//         // Information panel handling moved to separate class
//         // toggleInfoPanel(...);
//     }

//     private void EnableHighlight(ArAction action)
//     {
//         if (action?.properties == null) return;

//         // Cache property lookups
//         var colorHexObj = action.properties.GetValueOrDefault("colorHex", "#FFFFFF");
//         string colorHex = colorHexObj.ToString();

//         Color parsedColor;
//         if (ColorUtility.TryParseHtmlString(colorHex, out parsedColor))
//         {
//             // Ensure alpha is set to 1
//             parsedColor.a = 1f;
//         }

//         var subIDs = GetSubIDs(action);
//         if(subIDs != null)
//         {
//             foreach(string id in subIDs)
//             {
//                 if (debugEnableAllSettings)
//                 {
//                     toggleTransform(Markers, true, id, parsedColor);
//                     toggleTransform(rowIndicators, true, id[0].ToString(), parsedColor);
//                     toggleTransform(colIndicators, true, id.Substring(1), parsedColor);
//                 }
//                 else
//                 {
//                     toggleTransform(Markers, storedSettings[LablightSettings.Well_Indicators], id, parsedColor);
//                     toggleTransform(rowIndicators, storedSettings[LablightSettings.Relevant_RC_Only], id[0].ToString(), parsedColor);
//                     toggleTransform(colIndicators, storedSettings[LablightSettings.Relevant_RC_Only], id.Substring(1), parsedColor);
//                 }
//             }
//         }
//     }

//     public override void disablePreviousHighlight()
//     {
//         if (activeHighlights == null || activeHighlights.Count == 0) return;

//         modelActive = false;

//         foreach (var action in activeHighlights)
//         {
//             DisableHighlight(action);
//         }

//         toggleIndicators(false);
//         activeHighlights.Clear();
//     }

//     private void DisableHighlight(ArAction action)
//     {
//         if (action?.properties == null) return;

//         var subIDs = GetSubIDs(action);
//         if (subIDs != null)
//         {
//             foreach (string id in subIDs)
//             {
//                 toggleTransform(Markers, false, id);
//                 toggleTransform(rowIndicators, true, id[0].ToString(), defaultIndicatorColor);
//                 toggleTransform(colIndicators, true, id.Substring(1), defaultIndicatorColor);
//             }
//         }
//     }

//     private void toggleTransform(Transform parentTransform, bool value, string id = "", Color color = default)
//     {
//         if(parentTransform != null)
//         {
//             if(!String.IsNullOrEmpty(id))
//             {
//                 GameObject childObject = parentTransform.Find(id).gameObject;
//                 childObject.SetActive(value);
//                 if(color != default)
//                 {
//                     if(childObject.TryGetComponent<MeshRenderer>(out MeshRenderer ren))
//                     {
//                         ren.material.color = color;
//                     }
//                     else if(childObject.TryGetComponent<TextMeshProUGUI>(out TextMeshProUGUI tmp))
//                     {
//                         tmp.color = color;
//                     }
//                 }
//             }else
//             {
//                 parentTransform.gameObject.SetActive(value);
//             }
//         }
//     }

//     private void toggleIndicators(bool value)
//     {
//         if(rowIndicators != null && colIndicators != null)
//         {
//             foreach(Transform obj in rowIndicators)
//             {
//                 if(debugEnableAllSettings)
//                 {
//                     obj.GetComponent<TextMeshProUGUI>().color = defaultIndicatorColor;
//                 }
//                 else if(!storedSettings[LablightSettings.Relevant_RC_Only] && storedSettings[LablightSettings.RC_Markers])
//                 {
//                     obj.GetComponent<TextMeshProUGUI>().color = defaultIndicatorColor;
//                 }

//                 if(obj.GetComponent<TextMeshProUGUI>().color == defaultIndicatorColor)
//                 {
//                     obj.gameObject.SetActive(value);
//                 }
//             }
//             foreach(Transform obj in colIndicators)
//             {
//                 if (debugEnableAllSettings)
//                 {
//                     obj.GetComponent<TextMeshProUGUI>().color = defaultIndicatorColor;
//                 }
//                 else if (!storedSettings[LablightSettings.Relevant_RC_Only] && storedSettings[LablightSettings.RC_Markers])
//                 {
//                     obj.GetComponent<TextMeshProUGUI>().color = defaultIndicatorColor;
//                 }

//                 if(obj.GetComponent<TextMeshProUGUI>().color == defaultIndicatorColor)
//                 {
//                     obj.gameObject.SetActive(value);
//                 }
//             }
//         }

//     }


//     // private void toggleInfoPanel(bool value, List<HighlightAction> actions) //info panel takes in 1-2 actions and prints the necessary info on the information panel based on the information provided in the highlight actions provided
//     // {
//     //     if(infoPanel != null)
//     //     {
//     //         infoPanel.gameObject.SetActive(value);
//     //         if(value && actions != null)
//     //         {
//     //             string colorHex = actions[0].colorInfo.Item1;
//     //             Color color;
//     //             if(ColorUtility.TryParseHtmlString(actions[0].colorInfo.Item1, out color))
//     //             {
//     //                 //color.a = 255;
//     //             }
//     //             foreach(Transform child in infoPanel.GetComponentInChildren<Transform>())
//     //             {
//     //                 switch(child.name)
//     //                 {
//     //                     //ignoring actions = 2 for now
//     //                     case "InfoDisplay":
//     //                         if(actions[0].actionName.Contains("transfer") && actions.Count() == 1)
//     //                         {
//     //                             if(actions[0].isSource)
//     //                             {
//     //                                 child.GetComponent<TextMeshProUGUI>().text = "Aliquot " + actions[0].volume.Item1.ToString("0.00") + actions[0].volume.Item2 + (actions[0].contents.Item2 != "" ? (" of " + "<" + colorHex + ">" + actions[0].contents.Item2 + "</color>") : "") + (actions[0].isSource ? " from " : " into ") + "<" + colorHex + ">" + "Well " + (actions[0].chainIDs.Count() > 1 ? (actions[0].chainIDs[0] + "-" + actions[0].chainIDs[actions[0].chainIDs.Count()-1]) : actions[0].chainIDs[0]) + "</color>";
//     //                             }
//     //                             else
//     //                             {
//     //                                 child.GetComponent<TextMeshProUGUI>().text = "Transfer " + actions[0].volume.Item1.ToString("0.00") + actions[0].volume.Item2 + (actions[0].contents.Item2 != "" ? (" of " + "<" + colorHex + ">" + actions[0].contents.Item2 + "</color>") : "") + (actions[0].isSource ? " from " : " into ") + "<" + colorHex + ">" + "Well " + (actions[0].chainIDs.Count() > 1 ? (actions[0].chainIDs[0] + "-" + actions[0].chainIDs[actions[0].chainIDs.Count()-1]) : actions[0].chainIDs[0]) + "</color>";
//     //                             }
//     //                         }else
//     //                         {
//     //                             child.GetComponent<TextMeshProUGUI>().text = "Transfer " + actions[0].volume.Item1.ToString("0.00") + actions[0].volume.Item2 + (actions[0].contents.Item2 != "" ? (" of <" + colorHex + ">" + actions[0].contents.Item2 + "</color>") : "") + (actions[0].isSource ? " from " : " into ") + "<" + colorHex + ">" + "Well " + (actions[0].chainIDs.Count() > 1 ? (actions[0].chainIDs[0] + "-" + actions[0].chainIDs[actions[0].chainIDs.Count()-1]) : actions[0].chainIDs[0]) + "</color>";
//     //                         }
//     //                         break;
//     //                     default:
//     //                         break;
//     //                 }
//     //             }
//     //         }
//     //     }
//     // }

//     public void toggleActiveComponents(bool value)
//     {
//         if (currActions != null)
//         {
//             foreach (var action in currActions)
//             {
//                 var subIDs = GetSubIDs(action);
//                 if (subIDs != null)
//                 {
//                     foreach (string id in subIDs)
//                     {
//                         if (debugEnableAllSettings)
//                         {
//                             toggleTransform(rowIndicators, true && value, id.Substring(0, 1));
//                             toggleTransform(colIndicators, true && value, id.Substring(1));
//                             toggleTransform(Markers, true && value, id);
//                         }
//                         else
//                         {
//                             toggleTransform(rowIndicators, storedSettings[LablightSettings.Relevant_RC_Only] && value, id.Substring(0, 1));
//                             toggleTransform(colIndicators, storedSettings[LablightSettings.Relevant_RC_Only] && value, id.Substring(1));
//                             toggleTransform(Markers, storedSettings[LablightSettings.Well_Indicators] && value, id);
//                         }
//                     }
//                 }
//             }
//         }

//         toggleTransform(ModelName, value);
//         if(debugEnableAllSettings)
//         {
//             // toggleInfoPanel((true && value), currActions); // Info panel handling moved to separate class
//             toggleIndicators((true && value));
//         }
//         else
//         {
//             // toggleInfoPanel(storedSettings[LablightSettings.Wellplate_Info_Panel] && value, currActions); // Info panel handling moved to separate class
//             toggleIndicators(storedSettings[LablightSettings.RC_Markers] && value);
//         }
//     }

//     void OnCheckItemChanged()
//     {
//         if(!ProtocolState.Instance.HasCurrentChecklist())
//         {
//             return;
//         }
//         if (!disableComponents && ProtocolState.Instance.CurrentCheckNum == ProtocolState.Instance.CurrentChecklist.Count() - 1 && ProtocolState.Instance.CurrentCheckItemState.Value.IsChecked.Value) //if on last checked item disable all active components
//         {
//             //play audio for check item completed
//             toggleActiveComponents(false);
//             toggleTransform(Cover, true);
//             disableComponents = true;
//         }
//         else if(disableComponents)
//         {
//             toggleActiveComponents(true);
//             toggleTransform(Cover, false);
//             disableComponents = false;
//         }
//     }

//     void OnStepChanged(ProtocolState.StepState step)
//     {
//         prevCheckItem = step.CheckNum.Value;
//         toggleTransform(Cover, false);
//     }
//     private void AddSubscriptions()
//     {
//         settingsManagerSO.settingChanged.AddListener(settingChanged =>
//         {
//             switch(settingChanged.Item1)
//             {
//                 case LablightSettings.RC_Markers:
//                     toggleIndicators(settingChanged.Item2);
//                     break;
//                 case LablightSettings.Relevant_RC_Only:
//                     if(currActions != null)
//                     {
//                         foreach(var action in currActions)
//                         {
//                             var colorHex = action.properties.GetValueOrDefault("colorHex", "#FFFFFF").ToString();
            
//                             Color parsedColor;
//                             if(ColorUtility.TryParseHtmlString(colorHex, out parsedColor))
//                             {
//                                 parsedColor.a = 1f;
//                             }
                            
//                             var subIDs = GetSubIDs(action);
//                             if(subIDs != null)
//                             {
//                                 foreach(string id in subIDs)
//                                 {
//                                     if(!settingChanged.Item2 && storedSettings[LablightSettings.RC_Markers])
//                                     {
//                                         toggleTransform(rowIndicators, true, id.Substring(0,1), defaultIndicatorColor); 
//                                         toggleTransform(colIndicators, true, id.Substring(1), defaultIndicatorColor);
//                                     }
//                                     else
//                                     {
//                                         toggleTransform(rowIndicators, settingChanged.Item2, id.Substring(0,1), parsedColor);
//                                         toggleTransform(colIndicators, settingChanged.Item2, id.Substring(1), parsedColor);
//                                     }                                    
//                                 }
//                             }
//                         }
//                     }
//                     break;
//                 case LablightSettings.RC_Highlights:
//                     if(currActions != null)
//                     {
//                         foreach(var action in currActions)
//                         {
//                             var subIDs = GetSubIDs(action);
//                             if(subIDs != null)
//                             {
//                                 foreach(string id in subIDs)
//                                 {
//                                     toggleTransform(rowHighlights, settingChanged.Item2, id.Substring(0,1));
//                                     toggleTransform(colHighlights, settingChanged.Item2, id.Substring(1));
//                                 }
//                             }
//                         }
//                     }
//                     break;
//                 case LablightSettings.Wellplate_Info_Panel:
//                     // toggleInfoPanel(settingChanged.Item2, currActions); // Info panel handling moved to separate class
//                     break;
//                 case LablightSettings.Well_Indicators:
//                     if(currActions != null)
//                     {
//                         foreach(var action in currActions)
//                         {
//                             var colorHex = action.properties.GetValueOrDefault("colorHex", "#FFFFFF").ToString();
                            
//                             Color parsedColor;
//                             if(ColorUtility.TryParseHtmlString(colorHex, out parsedColor))
//                             {
//                                 parsedColor.a = 1f;
//                             }
//                             var subIDs = GetSubIDs(action);
//                             if(subIDs != null)
//                             {
//                                 foreach(string id in subIDs)
//                                 {
//                                     toggleTransform(Markers, settingChanged.Item2, id, parsedColor);
//                                 }
//                             }
//                         }
//                     }
//                     break;
//             }
//         });
//     }

//     public override void Rotate(float degrees)
//     {
//         Model.Rotate(Vector3.up, degrees);

//         foreach (Transform indicator in colIndicators)
//         {
//             indicator.Rotate(Vector3.forward, degrees);
//         }
//         foreach (Transform indicator in rowIndicators)
//         {
//             indicator.Rotate(Vector3.forward, degrees);
//         }
//     }


//     private List<string> GetSubIDs(ArAction action)
//     {
//         if (action == null || action.properties == null)
//             return null;

//         var subIDsObj = action.properties.GetValueOrDefault("subIDs", null);
//         if (subIDsObj == null)
//             return null;

//         if (subIDsObj is IEnumerable subIDsEnumerable && !(subIDsObj is string))
//         {
//             var subIDsList = new List<string>();
//             foreach (var idObj in subIDsEnumerable)
//             {
//                 if (idObj != null)
//                 {
//                     subIDsList.Add(idObj.ToString());
//                 }
//             }
//             return subIDsList;
//         }
//         else
//         {
//             return new List<string> { subIDsObj.ToString() };
//         }
//     }

// }
