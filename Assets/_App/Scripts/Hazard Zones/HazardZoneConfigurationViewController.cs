using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

public class HazardZoneConfigurationViewController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] Canvas canvas;
    [SerializeField] XRSimpleInteractable cubeButton;
    [SerializeField] XRSimpleInteractable cylinderButton;

    [SerializeField] XRSimpleInteractable warningButton;
    [SerializeField] XRSimpleInteractable dangerButton;

    [SerializeField] Slider sizeSlider;
    [SerializeField] TextMeshProUGUI sizeText;

    [SerializeField] XRSimpleInteractable placeZoneButton;

    [SerializeField] GameObject hazardTypeGroup;
    [SerializeField] GameObject sizeGroup;

    [Header("Materials")]
    [SerializeField] Material warningMaterial;
    [SerializeField] Material dangerMaterial;

    [SerializeField] Material toggleOnMaterial;
    [SerializeField] Material toggleOffMaterial;

    [Header("Meshes")]
    [SerializeField] Mesh cubeMesh;
    [SerializeField] Mesh cylinderMesh;

    [Header("Hazard zone prefab")]
    [SerializeField] GameObject hazardZonePrefab;

    private Material selectedMaterial;
    private Mesh selectedMesh;

    public void Start()
    {
        cubeButton.selectEntered.AddListener(_ => OnCubeSelected());
        cylinderButton.selectEntered.AddListener(_ => OnCylinderSelected());

        warningButton.selectEntered.AddListener(_ => OnWarningSelected());
        dangerButton.selectEntered.AddListener(_ => OnDangerSelected());

        placeZoneButton.selectEntered.AddListener(_ => OnPlaceZone());

        //slider doesnt work unless we refresh the canvas, Unity UI moment...
        StartCoroutine(RefreshCanvas());
    }

    public void OnCubeSelected()
    {
        selectedMesh = cubeMesh;
        cubeButton.GetComponent<MeshRenderer>().material = toggleOnMaterial;
        cylinderButton.GetComponent<MeshRenderer>().material = toggleOffMaterial;

        hazardTypeGroup.SetActive(true);
    }

    public void OnCylinderSelected()
    {
        selectedMesh = cylinderMesh;
        cylinderButton.GetComponent<MeshRenderer>().material = toggleOnMaterial;
        cubeButton.GetComponent<MeshRenderer>().material = toggleOffMaterial;

        hazardTypeGroup.SetActive(true);
    }

    public void OnWarningSelected()
    {
        selectedMaterial = warningMaterial;
        warningButton.GetComponent<MeshRenderer>().material = toggleOnMaterial;
        dangerButton.GetComponent<MeshRenderer>().material = toggleOffMaterial;

        sizeGroup.SetActive(true);
    }

    public void OnDangerSelected()
    {
        selectedMaterial = dangerMaterial;
        dangerButton.GetComponent<MeshRenderer>().material = toggleOnMaterial;
        warningButton.GetComponent<MeshRenderer>().material = toggleOffMaterial;

        sizeGroup.SetActive(true);
    }

    public void OnSliderValueChanged(float value)
    {
        sizeText.text = value.ToString("F2") + "m";

        placeZoneButton.gameObject.SetActive(true);
    }

    public void OnPlaceZone()
    {
        GameObject hazardZone = Instantiate(hazardZonePrefab);
        hazardZone.GetComponent<HazardZone>().Initalize(selectedMesh, selectedMaterial, sizeSlider.value);
        Destroy(gameObject);
    }

    IEnumerator RefreshCanvas()
    {
        yield return new WaitForSeconds(1f);
        canvas.gameObject.SetActive(false);
        canvas.gameObject.SetActive(true);
    }
}
