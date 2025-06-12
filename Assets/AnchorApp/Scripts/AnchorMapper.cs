using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class AnchorMapper : MonoBehaviour
{
    public ARAnchorManager m_AnchorManager;

    public TextMeshPro text;

    static readonly List<ARAnchor> k_AnchorsToDestroy = new();

    private void OnEnable()
    {
        m_AnchorManager.anchorsChanged += OnAnchorsChanged;
    }

    private void OnDisable()
    {
        m_AnchorManager.anchorsChanged -= OnAnchorsChanged;
    }

    void OnAnchorsChanged(ARAnchorsChangedEventArgs args)
    {
        foreach (var anchor in args.added)
        {
            //OnAnchorAdded(anchor);
        }

        foreach (var anchor in args.updated)
        {
            //OnAnchorUpdated(anchor);
        }

        foreach (var anchor in args.removed)
        {
            //OnAnchorRemoved(anchor);
        }

        text.text = "Anchors changed: " + args.added.Count + " added, " + args.updated.Count + " updated, " + args.removed.Count + " removed";
    }


    public void ClearWorldAnchors()
    {
        text.text = "Clearing world anchors";

        if (m_AnchorManager == null)
        {
            Debug.LogError("Cannot clear world anchors; Anchor Manager is null");
            return;
        }

        var anchorSubsystem = m_AnchorManager.subsystem;
        if (anchorSubsystem == null || !anchorSubsystem.running)
        {
            Debug.LogWarning("Cannot clear anchors if subsystem is not running");
            return;
        }

        // Copy anchors to a reusable list to avoid InvalidOperationException caused by Destroy modifying the list of anchors
        k_AnchorsToDestroy.Clear();
        foreach (var anchor in m_AnchorManager.trackables)
        {
            if (anchor == null)
                continue;

            k_AnchorsToDestroy.Add(anchor);
        }

        foreach (var anchor in k_AnchorsToDestroy)
        {
            Debug.Log($"Destroying anchor with trackable id: {anchor.trackableId.ToString()}");
            Destroy(anchor.gameObject);
        }

        k_AnchorsToDestroy.Clear();
    }
}
