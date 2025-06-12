using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniRx;
using TMPro;

public enum LockingType
{
    Image,
    Plane
}

[Serializable]
public class HighlightGroup
{
    public string Name;
    public List<GameObject> SubParts;
}

[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(AudioSource))]
public class ArObjectViewController : MonoBehaviour
{
    
    [SerializeField]
    public LockingType LockingType;
    public SettingsManagerScriptableObject settingsManagerSO;

    public string ObjectName;

    public GameObject sphere; // optional collision sphere

    public List<HighlightGroup> HighlightGroups;
    public ArObject arObject;

    // track highlight/selection states
    protected List<ArAction> activeHighlights = new List<ArAction>();
    protected List<ArAction> currentActions;
    protected bool modelActive;
    protected bool alignmentTriggered;

    // used to disable all interactive components
    protected bool disableComponents;

    public bool positionLocked = false;
    public bool hasBeenLocked = false;

    protected Vector3 _defaultPosition = Vector3.zero;
    protected Vector3 positionOnLock;

    // If WellPlateHighlightManager is attached, we'll handle rotating row/col indicators
    private WellPlateHighlightManager wellPlateHighlightManager;

    [SerializeField]
    public Transform ModelName;

    [SerializeField]
    private Transform Model;

    [SerializeField] protected Transform highlightPoints; // general sub-objects for highlighting

    // example markers/outlines from old classes
    [SerializeField] public Transform Outline;
    [SerializeField] public Transform nameTags;  // e.g. for Source or other objects

    // stream subscriptions
    private IDisposable stepStreamSub;
    private IDisposable checklistStreamSub;

    void Awake()
    {
        // Try to grab WellPlateHighlightManager if it exists on the same GameObject
        wellPlateHighlightManager = GetComponent<WellPlateHighlightManager>();

        stepStreamSub = ProtocolState.Instance.StepStream.Subscribe(OnStepChanged).AddTo(this);
        checklistStreamSub = ProtocolState.Instance.ChecklistStream.Subscribe(_ => OnCheckItemChanged()).AddTo(this);

        if (settingsManagerSO != null)
        {
            AddSettingsSubscriptions();
        }
    }

    public virtual void Initialize(ArObject arObject)
    {
        this.arObject = arObject;
        if (ModelName != null && arObject != null && !string.IsNullOrEmpty(arObject.specificObjectName))
        {
            var textMesh = ModelName.GetComponent<TextMeshProUGUI>();
            if (textMesh != null)
            {
                textMesh.text = arObject.specificObjectName;
                ModelName.gameObject.SetActive(true);
            }
        }
        transform.position = SessionManager.instance.CharucoTransform != null 
            ? SessionManager.instance.CharucoTransform.position
            : _defaultPosition;
    }

    // handle alignment visuals
    public virtual void AlignmentGroup()
    {
        if (!disableComponents)
        {
            alignmentTriggered = true;
            // show any alignment subparts
            HighlightGroups?.ForEach(hg => hg.SubParts?.ForEach(obj => obj.SetActive(true)));
        }
    }

    // revert alignment visuals
    public virtual void ResetToCurrentHighlights()
    {
        if (!disableComponents)
        {
            alignmentTriggered = false;
            // hide alignment subparts
            HighlightGroups?.ForEach(hg => hg.SubParts?.ForEach(obj => obj.SetActive(false)));

            if (currentActions != null && modelActive)
            {
                HighlightGroup(currentActions);
            }
        }
    }

    // highlight new group
    public virtual void HighlightGroup(List<ArAction> actions)
    {
        if (actions == null || actions.Count == 0 || alignmentTriggered) return;

        disableComponents = false;
        DisablePreviousHighlight();

        modelActive = true;
        activeHighlights = actions;
        currentActions = actions;

        foreach (var action in actions)
        {
            EnableHighlight(action);
        }
    }

    // remove old highlights
    public virtual void DisablePreviousHighlight()
    {
        if (activeHighlights == null || activeHighlights.Count == 0) return;
        modelActive = false;

        foreach (var action in activeHighlights)
        {
            DisableHighlight(action);
        }
        activeHighlights.Clear();
    }

    protected virtual void EnableHighlight(ArAction action)
    {
        if (action?.properties == null) return;

        var colorHex = action.properties.GetValueOrDefault("colorHex", "#FFFFFF").ToString();
        Color parsedColor = ParseColor(colorHex);

        var subIDs = GetSubIDs(action);
        if (subIDs != null)
        {
            // highlight the subIDs
            foreach (string id in subIDs)
            {
                ToggleTransform(highlightPoints, true, id, parsedColor);
                if(Outline != null)
                {
                    ToggleTransform(Outline, true, id, parsedColor);
                }
                if(nameTags != null)
                {
                    ToggleTransform(nameTags, true, id, parsedColor);
                }
            }
        }
    }

    protected virtual void DisableHighlight(ArAction action)
    {
        var subIDs = GetSubIDs(action);
        if (subIDs != null)
        {
            foreach (string id in subIDs)
            {
                // turn off sub object
                ToggleTransform(highlightPoints, false, id);
                if(Outline != null)
                {
                    ToggleTransform(Outline, false, id);
                }
                if(nameTags != null)
                {
                    ToggleTransform(nameTags, false, id);
                }
            }
        }
    }

    // simpler toggling for sub-objects
    protected void ToggleTransform(Transform parentTransform, bool value, string id = "", Color color = default)
    {
        if (parentTransform == null) return;
        if (!string.IsNullOrEmpty(id))
        {
            var child = parentTransform.Find(id)?.gameObject;
            if (child == null) return;
            child.SetActive(value);
            if (color != default && child.TryGetComponent<MeshRenderer>(out var ren))
            {
                ren.material.color = color;
            }
            else if (color != default && child.TryGetComponent<TextMeshProUGUI>(out var text))
            {
                text.color = color;
            }
        }
        else
        {
            parentTransform.gameObject.SetActive(value);
        }
    }

    protected Color ParseColor(string colorHex)
    {
        Color parsedColor;
        if (ColorUtility.TryParseHtmlString(colorHex, out parsedColor))
        {
            parsedColor.a = 1f;
        }
        return parsedColor;
    }

    // lock/unlock model in place
    public void UnlockPosition() => positionLocked = false;
    public void LockPosition()
    {
        hasBeenLocked = true;
        positionLocked = true;
        if (arObject != null && !string.IsNullOrEmpty(arObject.rootPrefabName))
        {
            transform.localPosition = new Vector3(transform.localPosition.x, 0, transform.localPosition.z);
        }
        positionOnLock = transform.localPosition;

        var headPlacementEventChannel = ServiceRegistry.GetService<HeadPlacementEventChannel>();
        headPlacementEventChannel?.CurrentPrefabLocked?.Invoke();
    }

    // step or checklist changed
    void OnCheckItemChanged()
    {
        if (!ProtocolState.Instance.HasCurrentChecklist()) return;

        // Example logic: if last item is checked, disable active components
        bool onLastItem = (ProtocolState.Instance.CurrentCheckNum == ProtocolState.Instance.CurrentChecklist.Count() - 1)
                            && ProtocolState.Instance.CurrentCheckItemState.Value.IsChecked.Value;
        if (onLastItem && !disableComponents)
        {
            ToggleActiveComponents(false);
            disableComponents = true;
        }
        else if (disableComponents && !onLastItem)
        {
            ToggleActiveComponents(true);
            disableComponents = false;
        }
    }

    void OnStepChanged(ProtocolState.StepState step)
    {
        // logic for stepping, e.g. re-enable components if needed
        // or hide alignment covers, etc.
        // example usage:
        //ToggleTransform(sphere?.transform, false);
    }

    // simpler approach to toggling off relevant components
    protected void ToggleActiveComponents(bool value)
    {
        if (currentActions != null)
        {
            foreach (var action in currentActions)
            {
                var subIDs = GetSubIDs(action);
                if (subIDs != null)
                {
                    foreach (string id in subIDs)
                    {
                        ToggleTransform(highlightPoints, value, id);
                        if(Outline != null)
                        {
                            ToggleTransform(Outline, value, id);
                        }
                        if(nameTags != null)
                        {
                            ToggleTransform(nameTags, value, id);
                        }
                    }
                }
            }
        }
        // show/hide model name
        ToggleTransform(ModelName, value);
    }

    protected virtual void AddSettingsSubscriptions()
    {
        if (settingsManagerSO == null) return;
        settingsManagerSO.settingChanged.AddListener(settingChanged =>
        {
            // handle general highlight toggles
            switch (settingChanged.Item1)
            {
                case LablightSettings.Well_Indicators:
                    // turn on/off highlightPoints if needed
                    if (currentActions != null && modelActive)
                    {
                        foreach (var action in currentActions)
                        {
                            var subIDs = GetSubIDs(action);
                            if (subIDs == null) continue;
                            var colorHex = action.properties.GetValueOrDefault("colorHex", "#FFFFFF").ToString();
                            Color parsedColor = ParseColor(colorHex);

                            foreach (string id in subIDs)
                            {
                                ToggleTransform(highlightPoints, settingChanged.Item2, id, parsedColor);
                            }
                        }
                    }
                    break;
            }
        });
    }

    public virtual void Rotate(float degrees)
    {
        // Basic rotation on Model if present
        if (Model != null) Model.Rotate(Vector3.up, degrees);

        // If there's a WellPlateHighlightManager on this object, rotate row/column indicators
        wellPlateHighlightManager?.RotateIndicators(degrees);
    }

    private List<string> GetSubIDs(ArAction action)
    {
        if (action == null || action.properties == null) return null;
        var subIDsObj = action.properties.GetValueOrDefault("subIDs", null);
        if (subIDsObj == null) return null;

        if (subIDsObj is IEnumerable<object> subIDsEnumerable && !(subIDsObj is string))
        {
            var subIDsList = new List<string>();
            foreach (var idObj in subIDsEnumerable)
            {
                if (idObj != null)
                {
                    subIDsList.Add(idObj.ToString());
                }
            }
            return subIDsList;
        }
        else
        {
            return new List<string> { subIDsObj.ToString() };
        }
    }

    void OnDestroy()
    {
        stepStreamSub?.Dispose();
        checklistStreamSub?.Dispose();
    }

    public override string ToString()
    {
        return $"ArObjectViewController: {ObjectName}";
    }
}