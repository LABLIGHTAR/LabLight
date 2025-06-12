using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Utilities;

/// <summary>
/// Behavior with an API for spawning an object.
///
///
/// -deleting anchored object when stored in persistent scene
/// -data retrieval and init
/// -billboarding
/// -recording text, rewrite
/// 
/// </summary>
public class ObjectSpawner : MonoBehaviour
{
    [SerializeField]
    [Tooltip("The camera that objects will face when spawned. If not set, defaults to the main camera.")]
    Camera m_CameraToFace;

    /// <summary>
    /// The camera that objects will face when spawned. If not set, defaults to the <see cref="Camera.main"/> camera.
    /// </summary>
    public Camera cameraToFace
    {
        get
        {
            EnsureFacingCamera();
            return m_CameraToFace;
        }
        set => m_CameraToFace = value;
    }

    [SerializeField]
    [Tooltip("The list of prefabs available to spawn.")]
    GameObject m_ObjectPrefab;

    /// <summary>
    /// The list of prefabs available to spawn.
    /// </summary>
    public GameObject objectPrefab
    {
        get => m_ObjectPrefab;
        set => m_ObjectPrefab = value;
    }

    [SerializeField]
    [Tooltip("Optional prefab to spawn for each spawned object. Use a prefab with the Destroy Self component to make " +
        "sure the visualization only lives temporarily.")]
    GameObject m_SpawnVisualizationPrefab;

    /// <summary>
    /// Optional prefab to spawn for each spawned object.
    /// </summary>
    /// <remarks>Use a prefab with <see cref="DestroySelf"/> to make sure the visualization only lives temporarily.</remarks>
    public GameObject spawnVisualizationPrefab
    {
        get => m_SpawnVisualizationPrefab;
        set => m_SpawnVisualizationPrefab = value;
    }

    [SerializeField]
    [Tooltip("Whether to only spawn an object if the spawn point is within view of the camera.")]
    bool m_OnlySpawnInView = true;

    [SerializeField]
    [Tooltip("Do an overlap sphere check to see if the placement area is not already occupied.")]
    bool m_CheckPlacementArea = true;

    /// <summary>
    /// Whether to only spawn an object if the spawn point is within view of the <see cref="cameraToFace"/>.
    /// </summary>
    public bool onlySpawnInView
    {
        get => m_OnlySpawnInView;
        set => m_OnlySpawnInView = value;
    }

    [SerializeField]
    [Tooltip("The size, in viewport units, of the periphery inside the viewport that will not be considered in view.")]
    float m_ViewportPeriphery = 0.15f;

    [SerializeField]
    [Tooltip("")]
    float m_DelayTime;

    [FormerlySerializedAs("m_SphereRadius")]
    [SerializeField]
    [Tooltip("Sphere radius in the overlap sphere to check object placement.")]
    float m_OverlapSphereRadius = 0.2f;

    bool m_Spawning = false;
    Collider[] m_OverlapColliders;
    SphereCollider[] m_Colliders;
    LayerMask m_OverlapLayerMask;

    const string k_ObjectLayerName = "PlacementObject";

    /// <summary>
    /// The size, in viewport units, of the periphery inside the viewport that will not be considered in view.
    /// </summary>
    public float viewportPeriphery
    {
        get => m_ViewportPeriphery;
        set => m_ViewportPeriphery = value;
    }

    /// <summary>
    /// Event invoked after an object is spawned.
    /// </summary>
    /// <seealso cref="TrySpawnObject"/>
    public event Action<GameObject> objectSpawned;

    /// <summary>
    /// See <see cref="MonoBehaviour"/>.
    /// </summary>
    void Awake()
    {
        EnsureFacingCamera();
        m_OverlapLayerMask = 1 << LayerMask.NameToLayer(k_ObjectLayerName);
    }

    void EnsureFacingCamera()
    {
        if (m_CameraToFace == null)
            m_CameraToFace = Camera.main;
    }

    private void Update()
    {
#if UNITY_EDITOR

        if (Input.GetKeyDown(KeyCode.F1))
        {
            TrySpawnObject(Camera.main.transform.position + Camera.main.transform.forward, Vector3.up);
        }
#endif
    }

    /// <summary>
    /// Attempts to spawn an object from <see cref="objectPrefabs"/> at the given position. The object will have a
    /// yaw rotation that faces <see cref="cameraToFace"/>/>.
    /// </summary>
    /// <param name="spawnPoint">The world space position at which to spawn the object.</param>
    /// <returns>Returns <see langword="true"/> if the spawner successfully spawned an object. Otherwise returns
    /// <see langword="false"/>, for instance if the spawn point is out of view of the camera.</returns>
    /// <remarks>
    /// The object selected to spawn is based on <see cref="spawnOptionIndex"/>. If the index is outside
    /// the range of <see cref="objectPrefabs"/>, this method will select a random prefab from the list to spawn.
    /// Otherwise, it will spawn the prefab at the index.
    /// </remarks>
    /// <seealso cref="objectSpawned"/>
    public bool TrySpawnObject(Vector3 spawnPoint, Vector3 spawnNormal)
    {
        if (m_Spawning)
            return false;

        if (m_OnlySpawnInView)
        {
            var inViewMin = m_ViewportPeriphery;
            var inViewMax = 1f - m_ViewportPeriphery;
            var pointInViewportSpace = cameraToFace.WorldToViewportPoint(spawnPoint);
            if (pointInViewportSpace.z < 0f || pointInViewportSpace.x > inViewMax || pointInViewportSpace.x < inViewMin ||
                pointInViewportSpace.y > inViewMax || pointInViewportSpace.y < inViewMin)
            {
                return false;
            }
        }

        if (m_CheckPlacementArea)
        {
            var placedObject = Physics.OverlapSphereNonAlloc(spawnPoint, m_OverlapSphereRadius, m_OverlapColliders, m_OverlapLayerMask);
            if (placedObject > 0)
            {
                return false;
            }
        }

        var newObject = Instantiate(m_ObjectPrefab, spawnPoint, Quaternion.identity, this.transform);

        EnsureFacingCamera();
        var facePosition = m_CameraToFace.transform.position;
        var forward = facePosition - spawnPoint;
        BurstMathUtility.ProjectOnPlane(forward, spawnNormal, out var projectedForward);
        var projectedAngle = Vector3.SignedAngle(Vector3.forward, projectedForward, spawnNormal);
        var spawnEulerAngles = new Vector3(0, projectedAngle, 0);

        // Disable Colliders temporarily
        m_Colliders = newObject.GetComponentsInChildren<SphereCollider>();
        foreach (SphereCollider collider in m_Colliders)
            collider.enabled = false;
        StartCoroutine(DelayColliders(1.0f));

        if (m_SpawnVisualizationPrefab != null)
        {
            var visualizationTrans = Instantiate(m_SpawnVisualizationPrefab).transform;
            visualizationTrans.position = spawnPoint;
            visualizationTrans.eulerAngles = spawnEulerAngles;
        }

        objectSpawned?.Invoke(newObject);
        StartCoroutine(DelaySpawn(m_DelayTime));
        return true;
    }

    IEnumerator DelaySpawn(float seconds)
    {
        m_Spawning = true;
        yield return new WaitForSeconds(seconds);
        m_Spawning = false;
    }

    IEnumerator DelayColliders(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        foreach (Collider collider in m_Colliders)
            collider.enabled = true;
    }
}

