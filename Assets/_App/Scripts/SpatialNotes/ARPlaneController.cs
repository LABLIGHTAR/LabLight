using UnityEngine;
using UniRx;
using UnityEngine.XR.ARFoundation;

public class ARPlaneController : MonoBehaviour
{
    private void Start()
    {
        SessionState.SpatialNoteEditMode.Subscribe(val =>
        {
            var meshCollider = this.GetComponent<Collider>();
            if (meshCollider)
            {
                meshCollider.enabled = val;
            }
        });
    }
}
