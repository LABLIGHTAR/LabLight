using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UniRx;

//TODO: ANCHORS NEED TO BE REWORKED TO USE THE NEW FILE MANAGER AND DATABASE

/// <summary>
/// AnchoredObjectController is a script that initializes the payload of this GameObject
/// Using the trackableId of the anchor a lookup is performed to find the corresponding LabLight data that is attached to this anchor
/// </summary>
[RequireComponent(typeof(ARAnchor))]
public class AnchoredObjectController : MonoBehaviour
{
    // public bool editMode = false;
    // public bool debugMode = false;

    // public GameObject[] editModeUI;
    // public GameObject[] debugModeUI;

    // [SerializeField]
    // private TextMeshProUGUI debugText;

    // [SerializeField]
    // private TextMeshProUGUI text;

    // private AnchorData anchorData;
    // private ARAnchor anchor;
    // private SpatialNoteAnchor note;
    // private IAnchorDataProvider anchorDataProvider;

    // private void Start()
    // {
    //     SessionState.SpatialNoteEditMode.Subscribe(val =>
    //     {
    //         editMode = val;
    //         UpdateView();
    //     }).AddTo(this);
    // }

    // public void OnEnable()
    // {
    //     anchor = this.GetComponent<ARAnchor>();

    //     // Find the LabLight data that corresponds to this anchor
    //     anchorDataProvider = ServiceRegistry.GetService<IAnchorDataProvider>();
    //     if (anchorDataProvider != null)
    //     {
    //         anchorDataProvider.GetOrCreateAnchorData().First().Subscribe((data) =>
    //         {
    //             anchorData = data;
    //             note = FindSpatialNoteData();
    //             if (note != null)
    //             {
    //                 debugText.text = " Found " + anchor.trackableId.ToString();
    //             }
    //             else
    //             {
    //                 note = new SpatialNoteAnchor()
    //                 {
    //                     id = anchor.trackableId.ToString(),
    //                     text = "Empty"
    //                 };

    //                 anchorData.anchors.Add(note);
    //                 anchorDataProvider.SaveAnchorData(anchorData);

    //                 debugText.text = "Not found " + anchor.trackableId.ToString();
    //             }

    //             text.text = note.text;
    //         });
    //     }        

    //     // Use to lookup data anchor.trackableId and determine what part to activate/prefab to instantiate
    //     UpdateView();
    // }

    // private SpatialNoteAnchor FindSpatialNoteData()
    // {
    //     foreach (var spatialNote in anchorData.anchors)
    //     {
    //         if (spatialNote.id.Equals(anchor.trackableId.ToString()))
    //         {
    //             return spatialNote;
    //         }
    //     }
    //     return null;
    // }

    // private void UpdateView()
    // {
    //     // Enable UI for editMode, delete button, grab interaction etc.
    //     foreach (GameObject obj in editModeUI)
    //     {
    //         obj.SetActive(editMode);
    //     }

    //     // Enable UI for debugMode, show trackableId, show anchor position, show anchor rotation etc.
    //     foreach (GameObject obj in debugModeUI)
    //     {
    //         obj.SetActive(debugMode);
    //     }

    // }

    // public void OnValidate()
    // {
    //     UpdateView();
    // }

    // public void RemoveAnchoredObject()
    // {
    //     Destroy(this.gameObject);

    //     if (anchorDataProvider != null)
    //     {
    //         anchorData.anchors.Remove(note);
    //         anchorDataProvider.SaveAnchorData(anchorData);
    //     }
    // }

    // // Spatial note specific
    // public void Record()
    // {
    //     SpeechRecognizer.Instance.RecognizedTextHandler = HandleRecognizedText;
    // }

    // public void HandleRecognizedText(string recognizedText)
    // {
    //     Debug.Log(recognizedText);
    //     text.text = recognizedText;

    //     // save to json
    //     note.text = recognizedText;
    //     anchorDataProvider.SaveAnchorData(anchorData);

    //     SpeechRecognizer.Instance.RecognizedTextHandler = null;
    // }
}
