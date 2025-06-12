using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class ArObjectManager : MonoBehaviour
{
    public HeadPlacementEventChannel headPlacementEventChannel;

    private readonly Dictionary<ArObject, ArObjectViewController> arViews = new Dictionary<ArObject, ArObjectViewController>();
    private readonly Dictionary<string, GameObject> modelPrefabCache = new Dictionary<string, GameObject>();
    private readonly HashSet<string> lockedObjectIds = new HashSet<string>();
    
    private Transform workspaceTransform;
    private Coroutine placementCoroutine;
    private bool isInitialized;
    private int pendingArViewInitializations = 0;
    private IFileManager fileManager;
    
    // Locking system
    private readonly Queue<GameObject> lockingQueue = new Queue<GameObject>();
    private readonly Dictionary<LockingType, ILockingService> lockingServices = new Dictionary<LockingType, ILockingService>();
    private bool isLockingSessionActive = false;

    private void Awake()
    {
        InitializeSubscriptions();
    }

    private void Start()
    {
        InitializeLockingServices();
    }

    private void OnDisable()
    {
        ClearScene(true);
    }

    private void InitializeSubscriptions()
    {
        if (ProtocolState.Instance == null) return;

        ProtocolState.Instance.ProtocolStream
            .Subscribe(protocol => HandleProtocolChange(protocol))
            .AddTo(this);

        // ProtocolState.Instance.StepStream
        //     .Subscribe(_ => UpdateArActions())
        //     .AddTo(this);

        ProtocolState.Instance.ChecklistStream
            .Subscribe(_ => UpdateArActions())
            .AddTo(this);
        HandleProtocolChange(ProtocolState.Instance.ActiveProtocol.Value);
    }

    private void InitializeLockingServices()
    {
        // Get locking services from service registry
        var availableServices = ServiceRegistry.GetServices<ILockingService>();
        
        // Register services by their locking type
        foreach (var service in availableServices)
        {
            if (!lockingServices.ContainsKey(service.LockingType))
            {
                lockingServices[service.LockingType] = service;
                service.OnObjectLocked += OnObjectLocked;
                Debug.Log($"ArObjectManager: Registered {service.LockingType} locking service");
            }
        }
        
        // Alternative: Find services by component if not in registry
        if (lockingServices.Count == 0)
        {
            var planeService = FindObjectOfType<PlaneLockingService>();
            var imageService = FindObjectOfType<ImageLockingService>();
            
            if (planeService != null)
            {
                lockingServices[LockingType.Plane] = planeService;
                planeService.OnObjectLocked += OnObjectLocked;
                Debug.Log("ArObjectManager: Found PlaneLockingService component");
            }
            
            if (imageService != null)
            {
                lockingServices[LockingType.Image] = imageService;
                imageService.OnObjectLocked += OnObjectLocked;
                Debug.Log("ArObjectManager: Found ImageLockingService component");
            }
        }
        
        Debug.Log($"ArObjectManager: Initialized {lockingServices.Count} locking services");
    }

    private void HandleProtocolChange(ProtocolDefinition protocol)
    {
        ClearScene(true);
        if (protocol?.globalArObjects != null)
        {
            pendingArViewInitializations = protocol.globalArObjects.Count;
            InitializeArObjects(protocol.globalArObjects);
        }
    }

    private void InitializeArObjects(List<ArObject> arObjects)
    {
        if (!TryGetWorkspaceTransform()) return;

        foreach (var arObject in arObjects)
        {
            if (ValidateArObject(arObject))
            {
                CreateArView(arObject);
            }
        }
        isInitialized = true;
    }

    private bool TryGetWorkspaceTransform()
    {
        workspaceTransform = SessionState.WorkspaceTransform;
        if (workspaceTransform == null)
        {
            Debug.LogError("WorkspaceTransform not found");
            return false;
        }
        return true;
    }

    private bool ValidateArObject(ArObject arObject)
    {
        if (string.IsNullOrEmpty(arObject.rootPrefabName))
        {
            Debug.LogWarning($"Invalid ArObject: Missing rootPrefabName");
            return false;
        }
        return true;
    }

    private void CreateArView(ArObject arObject)
    {
        var prefabPath = $"Models/{arObject.rootPrefabName}";
        
        if (fileManager == null)
        {
            fileManager = ServiceRegistry.GetService<IFileManager>();
            if (fileManager == null)
            {
                Debug.LogError("[ArObjectManager] IFileManager service not found!");
                pendingArViewInitializations--;
                CheckInitializationComplete();
                return;
            }
        }

        fileManager.GetPrefabAsync(prefabPath)
            .ToObservable()
            .ObserveOnMainThread()
            .Subscribe(
                result => {
                    if (result.Success && result.Data != null)
                    {
                        InstantiateArView(result.Data, arObject);
                    }
                    else
                    {
                        Debug.LogError($"[ArObjectManager] Failed to load prefab {prefabPath}. Error: {result.Error?.Code} - {result.Error?.Message}");
                        pendingArViewInitializations--;
                        CheckInitializationComplete();
                    }
                },
                error => {
                    Debug.LogError($"[ArObjectManager] Exception during prefab loading observable for {prefabPath}: {error}");
                    pendingArViewInitializations--;
                    CheckInitializationComplete();
                }
            )
            .AddTo(this);
    }

    private void InstantiateArView(GameObject prefab, ArObject arObject)
    {
        if (!prefab.TryGetComponent<ArObjectViewController>(out var arViewPrefab))
        {
            Debug.LogError($"[ArObjectManager] Prefab {prefab.name} missing ArObjectViewController component");
            pendingArViewInitializations--;
            CheckInitializationComplete();
            return;
        }

        if (arViews.ContainsKey(arObject))
        {
            Destroy(arViews[arObject].gameObject);
        }

        var instance = Instantiate(arViewPrefab, workspaceTransform);
        instance.Initialize(arObject);
        instance.gameObject.SetActive(false);

        arViews[arObject] = instance;
        modelPrefabCache[arObject.arObjectID] = instance.gameObject;

        pendingArViewInitializations--;
        CheckInitializationComplete();
    }

    private void CheckInitializationComplete()
    {
        if (pendingArViewInitializations == 0)
        {
            isInitialized = true;
            UpdateArActions();
        }
    }

    private CheckItemDefinition previousCheckItem;
    private void UpdateArActions()
    {
        if(previousCheckItem != null && previousCheckItem == ProtocolState.Instance.CurrentCheckItemDefinition) return;
        previousCheckItem = ProtocolState.Instance.CurrentCheckItemDefinition;

        if (!isInitialized || !ProtocolState.Instance.HasCurrentCheckItem() || ProtocolState.Instance.CurrentStepState.Value.SignedOff.Value) return;

        var currentCheckItem = ProtocolState.Instance.CurrentCheckItemDefinition;
        if (currentCheckItem == null) return;

        ProcessArActions(currentCheckItem.arActions);
    }

    private void ProcessArActions(List<ArAction> actions)
    {    
        var lockActions = new List<ArAction>(); //actions.Where(a => a.actionType.ToLower() == "lock").ToList();
        var highlightActions = new Dictionary<string, List<ArAction>>();
        var placementActions = new List<ArAction>();

        foreach (var action in actions)
        {
            switch (action.actionType.ToLower())
            {
                case "lock":
                    lockActions.Add(action);
                    break;
                case "highlight":
                    if (!string.IsNullOrEmpty(action.arObjectID))
                    {
                        if (!highlightActions.ContainsKey(action.arObjectID))
                            highlightActions[action.arObjectID] = new List<ArAction>();
                        highlightActions[action.arObjectID].Add(action);
                    }
                    break;
                case "placement":
                    placementActions.Add(action);
                    break;
            }
        }
        if(lockActions.Count > 0)   
        {
            ProcessLockActions(lockActions);
        }
        if(highlightActions.Count > 0)  
        {
            ProcessHighlightActions(highlightActions);
        }
        if(placementActions.Count > 0)
        {
            ProcessPlacementActions(placementActions);
        }

        if (actions.Count == 0)
        {
            foreach(var arView in arViews)
            {
                if(arView.Value is ArObjectViewController vc)
                {
                    vc.DisablePreviousHighlight();
                }
            }
        }
    }

    private void ProcessLockActions(List<ArAction> lockActions)
    {
        var objectsToLock = new List<GameObject>();

        foreach (var action in lockActions)
        {
            if (!ValidateLockAction(action, out var arIDList)) continue;

            foreach (string id in arIDList)
            {
                if (modelPrefabCache.TryGetValue(id, out var prefab))
                {
                    objectsToLock.Add(prefab);
                    lockedObjectIds.Add(id);
                }
            }
        }

        if (objectsToLock.Count > 0)
        {
            EnqueueObjectsForLocking(objectsToLock);
        }
    }

    private bool ValidateLockAction(ArAction action, out List<string> arIDList)
    {
        arIDList = null;

        if (action.properties == null)
        {
            Debug.LogWarning($"Lock action properties are null: {action.arObjectID}");
            return false;
        }

        if (!action.properties.TryGetValue("arIDList", out var arIDListObj) || arIDListObj == null)
        {
            Debug.LogWarning($"Invalid arIDList in lock action: {action.arObjectID}");
            return false;
        }

        // If ArIDs for locking are created at time of Parsing they will be saved as a JArray when reserialized
        // Convert the JSON array to a List<string>
        if (arIDListObj is JArray jArray)
        {
            arIDList = jArray.ToObject<List<string>>();
        }
        else if (arIDListObj is List<object> objList)
        {
            arIDList = objList.Select(x => x?.ToString()).ToList();
        }

        // Filter out empty strings
        arIDList = arIDList?.Where(id => !string.IsNullOrEmpty(id)).ToList();
        
        return arIDList != null && arIDList.Count > 0;
    }

    private void ProcessHighlightActions(Dictionary<string, List<ArAction>> highlightActions)
    {
        foreach (var arView in arViews)
        {
            if (arView.Value is ArObjectViewController modelView)
            {
                var arObjectId = arView.Key.arObjectID;
                if (highlightActions.TryGetValue(arObjectId, out var actions))
                {
                    modelView.HighlightGroup(actions);
                }
                else
                {
                    modelView.DisablePreviousHighlight();
                }
            }
        }
    }

    private void ProcessPlacementActions(List<ArAction> placementActions)
    {
        foreach (var action in placementActions)
        {
            if (modelPrefabCache.TryGetValue(action.arObjectID, out var prefab))
            {
                RequestObjectPlacement(prefab);
            }
        }
    }

    private void RequestObjectPlacement(GameObject model)
    {
        if (placementCoroutine != null)
        {
            StopCoroutine(placementCoroutine);
        }
        placementCoroutine = StartCoroutine(StartObjectPlacement(model));
    }

    private IEnumerator StartObjectPlacement(GameObject model)
    {
        yield return new WaitForSeconds(0.36f);
        if (model != null)
        {
            headPlacementEventChannel.SetHeadtrackedObject.Invoke(model);
        }
        placementCoroutine = null;
    }

    private void ClearScene(bool clearLockedObjects = false)
    {
        // Cancel any active locking operations
        CancelLockingSession();
        
        foreach (var view in arViews.Values)
        {
            if (view != null)
            {
                Destroy(view.gameObject);
            }
        }
        
        arViews.Clear();
        modelPrefabCache.Clear();
        
        if (clearLockedObjects)
        {
            lockedObjectIds.Clear();
            ProtocolState.Instance.AlignmentTriggered.Value = false;
        }
        
        isInitialized = false;
    }


    private void UpdateArActionsForObject(ArObject arObject, ArObjectViewController instance)
    {
        if (!ProtocolState.Instance.HasCurrentCheckItem()) return;

        var currentCheckItem = ProtocolState.Instance.CurrentCheckItemDefinition;
        if (currentCheckItem == null) return;

        // Return early if check item hasn't changed
        if (previousCheckItem != null && previousCheckItem == currentCheckItem) return;
        previousCheckItem = currentCheckItem;

        // Filter actions that target this specific object
        var relevantActions = currentCheckItem.arActions
            .Where(action => action.arObjectID == arObject.arObjectID)
            .ToList();

        if (relevantActions.Count > 0)
        {
            ProcessArActions(relevantActions);
        }
        foreach(var arView in arViews)
        {
            if(arView.Key.arObjectID != arObject.arObjectID)
            {
                if(arView.Value is ArObjectViewController modelView)
                {
                    modelView.DisablePreviousHighlight();
                }
            }
        }
    }

    #region Locking System Methods

    /// <summary>
    /// Enqueues objects for locking and starts the locking session
    /// </summary>
    /// <param name="objectsToLock">List of GameObjects to lock</param>
    private void EnqueueObjectsForLocking(List<GameObject> objectsToLock)
    {
        if (objectsToLock == null || objectsToLock.Count == 0) return;

        Debug.Log($"ArObjectManager: Enqueuing {objectsToLock.Count} objects for locking");

        // Clear existing queue and cancel any active locking
        CancelLockingSession();

        // Add objects to queue
        foreach (var obj in objectsToLock)
        {
            lockingQueue.Enqueue(obj);
        }

        // Start locking session
        isLockingSessionActive = true;
        ProtocolState.Instance.LockingTriggered.Value = true;

        // Begin locking the first object
        ProcessNextObjectInQueue();
    }

    /// <summary>
    /// Processes the next object in the locking queue
    /// </summary>
    private void ProcessNextObjectInQueue()
    {
        if (lockingQueue.Count == 0)
        {
            EndLockingSession();
            return;
        }

        var nextObject = lockingQueue.Dequeue();
        var arObjectViewController = nextObject.GetComponent<ArObjectViewController>();
        
        if (arObjectViewController == null)
        {
            Debug.LogWarning($"ArObjectManager: Object {nextObject.name} missing ArObjectViewController, skipping");
            ProcessNextObjectInQueue();
            return;
        }

        var lockingType = arObjectViewController.LockingType;
        
        if (lockingServices.TryGetValue(lockingType, out var lockingService))
        {
            Debug.Log($"ArObjectManager: Starting {lockingType} locking for {nextObject.name}");
            lockingService.BeginLocking(nextObject);
        }
        else
        {
            Debug.LogError($"ArObjectManager: No locking service found for type {lockingType}");
            ProcessNextObjectInQueue();
        }
    }

    /// <summary>
    /// Called when a locking service successfully locks an object
    /// </summary>
    /// <param name="lockedObject">The object that was locked</param>
    private void OnObjectLocked(GameObject lockedObject)
    {
        Debug.Log($"ArObjectManager: Object {lockedObject.name} successfully locked. {lockingQueue.Count} objects remaining.");
        
        // Process next object in queue
        ProcessNextObjectInQueue();
    }

    /// <summary>
    /// Ends the current locking session
    /// </summary>
    private void EndLockingSession()
    {
        Debug.Log("ArObjectManager: Ending locking session");
        
        isLockingSessionActive = false;
        ProtocolState.Instance.LockingTriggered.Value = false;
        
        // Cancel any active locking operations
        foreach (var service in lockingServices.Values)
        {
            if (service.IsLocking)
            {
                service.CancelLocking();
            }
        }
        
        lockingQueue.Clear();
    }

    /// <summary>
    /// Cancels the current locking session and clears the queue
    /// </summary>
    private void CancelLockingSession()
    {
        if (!isLockingSessionActive) return;
        
        Debug.Log("ArObjectManager: Cancelling locking session");
        
        // Cancel all active locking operations
        foreach (var service in lockingServices.Values)
        {
            if (service.IsLocking)
            {
                service.CancelLocking();
            }
        }
        
        lockingQueue.Clear();
        isLockingSessionActive = false;
        ProtocolState.Instance.LockingTriggered.Value = false;
    }

    /// <summary>
    /// Gets the current status of the locking system
    /// </summary>
    public bool IsLockingSessionActive => isLockingSessionActive;

    /// <summary>
    /// Gets the number of objects remaining in the locking queue
    /// </summary>
    public int RemainingObjectsToLock => lockingQueue.Count;

    #endregion
}