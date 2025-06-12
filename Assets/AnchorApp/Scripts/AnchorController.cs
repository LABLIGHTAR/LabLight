using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class AnchorController : MonoBehaviour
{
    public string DebugText = "Default";
    public TextMeshPro text;
    private ARAnchor anchor;

    // Start is called before the first frame update
    void Start()
    {
        anchor = this.GetComponent<ARAnchor>();   
    }

    // Update is called once per frame
    void Update()
    {
        text.text = DebugText + anchor.trackingState.ToString() + "  " +  anchor.trackableId.ToString();
    }

    public void Remove()
    {
       
    }
}
