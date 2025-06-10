using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelTransformBehavior : MonoBehaviour
{
    [SerializeField]
    private Transform parentPanel;

    GameObject m_TempParent;

    void Start()
    {
        m_TempParent = new GameObject("tempParent_" + gameObject.name);
        m_TempParent.transform.position = transform.position;
    }   

    public void Select()
    {
        transform.SetParent(m_TempParent.transform);
        Debug.Log("Selected");
    }

    public void Drag()
    {
        Debug.Log("Dragging");
        //m_TempParent.transform.LookAt(hmdPos);
        //parentPanel.LookAt(hmdPos);

        //m_TempParent.transform.position = interactionPosition;
        //Vector3 targetPanelPosition = new Vector3(interactionPosition.x, interactionPosition.y, interactionPosition.z) + parentPanel.up * 0.2f;

        Vector3 targetPanelPosition = new Vector3(m_TempParent.transform.position.x, m_TempParent.transform.position.y, m_TempParent.transform.position.z) + parentPanel.up * 0.2f;
        parentPanel.position = targetPanelPosition;
    }

    public void Deselect()
    {
        transform.SetParent(parentPanel);
        Debug.Log("Deselected");
    }
}
