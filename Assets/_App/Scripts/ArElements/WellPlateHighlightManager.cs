using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UniRx;
using System;

public class WellPlateHighlightManager : MonoBehaviour
{
    // Row & column indicator transforms
    public Transform rowIndicators;
    public Transform colIndicators;
    public Color defaultIndicatorColor = Color.white;

    private SettingsManagerScriptableObject settingsManagerSO;
    
    // Reference to the current set of highlight actions
    private List<ArAction> currentActions;

    private IDisposable checkStream;
    
    // Indicates if the model (and thus row/col highlights) is currently active
    private bool modelActive;

    private void Awake()
    {
        InitializeSubscriptions();
    }

    // Initialize the highlight manager with its required settings
    public void Initialize(SettingsManagerScriptableObject settingsManager)
    {
        settingsManagerSO = settingsManager;
        AddSettingsSubscriptions();
    }

    private void InitializeSubscriptions()
    {
        if (ProtocolState.Instance == null) return;

        // Subscribe to checklist changes
        checkStream = ProtocolState.Instance.ChecklistStream
            .Subscribe(_ => UpdateHighlights())
            .AddTo(this);
    }

    void OnDestroy()
    {
        checkStream?.Dispose();
    }

    private void UpdateHighlights()
    {
        if (!ProtocolState.Instance.HasCurrentCheckItem()) 
        {
            ClearAllHighlights();
            return;
        }

        var currentCheckItem = ProtocolState.Instance.CurrentCheckItemDefinition;
        if (currentCheckItem == null || currentCheckItem.arActions == null) 
        {
            ClearAllHighlights();
            return;
        }

        bool onLastItem = (ProtocolState.Instance.CurrentCheckNum == ProtocolState.Instance.CurrentChecklist.Count - 1)
                    && ProtocolState.Instance.CurrentCheckItemState.Value.IsChecked.Value;

        if(ProtocolState.Instance.CurrentCheckNum == ProtocolState.Instance.CurrentChecklist.Count - 1)
        {
            if(ProtocolState.Instance.CurrentCheckItemState.Value.IsChecked.Value)
            {
                ClearAllHighlights();
                return;
            }
        }

        // Update current actions and process them
        currentActions = currentCheckItem.arActions;
        ProcessHighlights();
    }

    private void ProcessHighlights()
    {
        // Clear existing highlights first
        ClearAllHighlights();

        // Apply new highlights
        foreach (var action in currentActions)
        {
            var subIDs = GetSubIDs(action);
            if (subIDs == null) continue;

            var colorHex = action.properties.GetValueOrDefault("colorHex", "#FFFFFF").ToString();
            var color = ParseColor(colorHex);

            foreach (var id in subIDs)
            {
                HighlightWellIndicators(id, color);
            }
        }
    }

    private void ClearAllHighlights()
    {
        if (rowIndicators == null || colIndicators == null) return;

        foreach (Transform indicator in rowIndicators)
        {
            ToggleIndicator(rowIndicators, indicator.name, defaultIndicatorColor, true);
        }

        foreach (Transform indicator in colIndicators)
        {
            ToggleIndicator(colIndicators, indicator.name, defaultIndicatorColor, true);
        }
    }

    // Set the actions (and active state) that determine which row/col indicators to highlight
    public void SetCurrentActions(List<ArAction> actions, bool isActive)
    {
        currentActions = actions;
        modelActive = isActive;
    }

    // Rotate row/column indicators around a forward axis
    public void RotateIndicators(float degrees)
    {
        rowIndicators?.Rotate(Vector3.forward, degrees);
        colIndicators?.Rotate(Vector3.forward, degrees);
    }

    // Subscribe to relevant settings changes to toggle row/column indicators
    private void AddSettingsSubscriptions()
    {
        if (settingsManagerSO == null) return;

        settingsManagerSO.settingChanged.AddListener(settingChanged =>
        {
            // Only apply changes if the model is active and we have actions
            if (!modelActive || currentActions == null) return;

            switch (settingChanged.Item1)
            {
                case LablightSettings.RC_Markers:
                    ToggleAllIndicators(rowIndicators, settingChanged.Item2);
                    ToggleAllIndicators(colIndicators, settingChanged.Item2);
                    break;

                case LablightSettings.Relevant_RC_Only:
                    ReHighlightRelevantRCOnly(settingChanged.Item2);
                    break;
            }
        });
    }

    // Re-apply row/column highlights after toggling the "Relevant_RC_Only" setting
    private void ReHighlightRelevantRCOnly(bool showRelevantOnly)
    {
        if (currentActions == null) return;

        foreach (var action in currentActions)
        {
            var subIDs = GetSubIDs(action);
            if (subIDs == null) continue;

            var colorHex = action.properties.GetValueOrDefault("colorHex", "#FFFFFF").ToString();
            var color = ParseColor(colorHex);

            foreach (var id in subIDs)
            {
                HighlightWellIndicators(
                    id,
                    color
                );
            }
        }
    }

    // Toggles all row/column indicators ON or OFF
    private void ToggleAllIndicators(Transform indicators, bool active)
    {
        if (indicators == null) return;

        foreach (Transform indicator in indicators)
        {
            indicator.gameObject.SetActive(active);

            if (indicator.TryGetComponent<TextMeshProUGUI>(out var text))
            {
                text.color = defaultIndicatorColor;
            }
        }
    }

    // Highlight and color the row and column indicators associated with a specific wellId
    public void HighlightWellIndicators(string wellId, Color color)
    {
        if (string.IsNullOrEmpty(wellId)) return;

        var row = wellId[0].ToString();
        var col = wellId.Substring(1);

        Debug.Log("Highlighting row" + row + "highlight col" + col);

        Color highlightColor = color;
        ToggleIndicator(rowIndicators, row, highlightColor, true);
        ToggleIndicator(colIndicators, col, highlightColor, true);
    }

    // Clear any previously highlighted row and column indicators for a specific wellId
    public void ClearWellIndicators(string wellId)
    {
        if (string.IsNullOrEmpty(wellId)) return;

        var row = wellId[0].ToString();
        var col = wellId.Substring(1);

        ToggleIndicator(rowIndicators, row, defaultIndicatorColor, true);
        ToggleIndicator(colIndicators, col, defaultIndicatorColor, true);
    }

    // Core toggle logic for individual row/column indicators
    private void ToggleIndicator(Transform indicators, string id, Color color, bool active)
    {
        if (indicators == null) return;
        var indicatorObj = indicators.Find(id);
        if (indicatorObj == null) return;

        indicatorObj.gameObject.SetActive(active);
        if (indicatorObj.TryGetComponent<TextMeshProUGUI>(out var text))
        {
            text.color = color;
        }
    }

    // Converts provided colorHex string into a Color object, defaulting to defaultIndicatorColor
    private Color ParseColor(string colorHex)
    {
        Color parsedColor = defaultIndicatorColor;
        if (ColorUtility.TryParseHtmlString(colorHex, out Color c))
        {
            c.a = 1f;
            parsedColor = c;
        }
        return parsedColor;
    }

    // Helper to extract row/column IDs from ArAction
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
} 