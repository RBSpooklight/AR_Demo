using UnityEngine;
using Vuforia;

//TODO: implement Singleton after merge
public sealed class ARManager : MonoBehaviour 
{	
	private enum EPlacementMode
	{
		Ground,
		MidAir
	}
	
	#region PRIVATE MEMBERS
	[Header("Vuforia")]
	[SerializeField] private PlaneFinderBehaviour m_planeFinder; 
	[SerializeField] private MidAirPositionerBehaviour m_midAirPositioner;
	
	[SerializeField] private GameObject m_augmentation;
	
	[Header("UI")]
	[SerializeField] private UnityEngine.UI.Button m_midAirToggle;
	[SerializeField] private UnityEngine.UI.Button m_groundToggle;
	[SerializeField] private UnityEngine.UI.Button m_resetButton;
	
	[SerializeField] private CanvasGroup m_screenReticleGround;
	
	//TODO: Remove, debug purposes
	[SerializeField] private UnityEngine.UI.Image m_positionalDeviceTrackerStatus;
	[SerializeField] private UnityEngine.UI.Image m_smartTerrainStatus;
	
	private StateManager m_stateManager;

	private EPlacementMode m_placementMode;
	
	private PositionalDeviceTracker m_positionalDeviceTracker;
	private SmartTerrain m_smartTerrain;

	private Camera m_mainCamera;

	private GameObject m_planeAnchor, m_midAirAnchor;

	private int m_anchorCounter;
	private int m_autoHitTestFrameCount;
	#endregion
	
	#region MONOBEHAVIOUR_METHODS
	private void Start() 
	{
		//Register callbacks
		VuforiaARController.Instance.RegisterVuforiaStartedCallback(OnVuforiaStarted);
		DeviceTrackerARController.Instance.RegisterTrackerStartedCallback(OnTrackerStarted);
		
		//Set default values
		m_mainCamera = Camera.main;
		m_planeFinder.HitTestMode = HitTestMode.AUTOMATIC;
		m_placementMode = EPlacementMode.Ground;
	}

	//TODO: Remove, debug purposes
	private void Update()
	{
		m_positionalDeviceTrackerStatus.color = Color.red;
		m_smartTerrainStatus.color = Color.red;
		
		if (m_positionalDeviceTracker.IsActive)
			m_positionalDeviceTrackerStatus.color = Color.green;
		
		if (m_smartTerrain.IsActive)
			m_smartTerrainStatus.color = Color.green;
			
	}
	
	private void LateUpdate()
    {
	    //We got an automatic hit test this frame
        if (m_autoHitTestFrameCount == Time.frameCount)
        {
            //Hide the screen reticle when we get a hit test
            m_screenReticleGround.alpha = 0;

            //Set the surface indicator to visible
            SetSurfaceIndicatorVisible(m_placementMode == EPlacementMode.Ground);
        }
        else
        {
            //No automatic hit test, so set alpha based on which plane mode is active
            m_screenReticleGround.alpha = (m_placementMode == EPlacementMode.Ground) ? 1 : 0;

	        //Set the surface indicator to invisible
            SetSurfaceIndicatorVisible(false);
        }
    }
	
	private void OnDestroy()
	{
		//Unregister Callbacks
		VuforiaARController.Instance.UnregisterVuforiaStartedCallback(OnVuforiaStarted);
		DeviceTrackerARController.Instance.UnregisterTrackerStartedCallback(OnTrackerStarted);
	}
	#endregion
	
	#region PUBLIC_UI_METHODS
	public void SetGroundMode()
	{
		if (m_placementMode == EPlacementMode.Ground)
			return;
	    
		m_placementMode = EPlacementMode.Ground;
		m_planeFinder.gameObject.SetActive(true);
		m_midAirPositioner.gameObject.SetActive(false);
    }

    public void SetMidAirMode()
    {
	    if (m_placementMode == EPlacementMode.MidAir)
		    return;
	    
		m_placementMode = EPlacementMode.MidAir;
		m_planeFinder.gameObject.SetActive(false);
		m_midAirPositioner.gameObject.SetActive(true);
    }

    public void ResetScene()
    {
        //First reset augmentations
        m_augmentation.transform.position = Vector3.zero;
        m_augmentation.transform.localEulerAngles = Vector3.zero;
        m_augmentation.SetActive(false);

        //Then reset placement mode
	    SetGroundMode();
    }
	
	public void ResetTrackers()
	{
		m_smartTerrain = TrackerManager.Instance.GetTracker<SmartTerrain>();
		m_positionalDeviceTracker = TrackerManager.Instance.GetTracker<PositionalDeviceTracker>();

		// Stop and restart trackers (KEEP CALL ORDER)
		m_smartTerrain.Stop();
		m_positionalDeviceTracker.Stop();
		m_positionalDeviceTracker.Start();
		m_smartTerrain.Start();
	}
	#endregion
	
	#region VUFORIA_CALLBACKS
	private void OnVuforiaStarted()
	{
		m_stateManager = TrackerManager.Instance.GetStateManager();

		//Check trackers to see if started, and start if necessary
		m_positionalDeviceTracker = TrackerManager.Instance.GetTracker<PositionalDeviceTracker>(); 
		m_smartTerrain = TrackerManager.Instance.GetTracker<SmartTerrain>();

		if (m_positionalDeviceTracker != null && m_smartTerrain != null)
		{
			if (!m_positionalDeviceTracker.IsActive)
				m_positionalDeviceTracker.Start();
			if (m_positionalDeviceTracker.IsActive && !m_smartTerrain.IsActive)
				m_smartTerrain.Start();
		}
		else
		{
			//TODO: Here we can define either the device supports GroundPlane or not.
			if (m_positionalDeviceTracker == null)
				Debug.Log("ARManager: PositionalDeviceTracker returned null. GroundPlane not supported on this device.");
			if (m_smartTerrain == null)
				Debug.Log("ARManager: SmartTerrain returned null. GroundPlane not supported on this device.");
		}
	}
	#endregion
	
	#region DEVICE_TRACKER_CALLBACKS
	private void OnTrackerStarted()
	{
		m_positionalDeviceTracker = TrackerManager.Instance.GetTracker<PositionalDeviceTracker>();
		m_smartTerrain = TrackerManager.Instance.GetTracker<SmartTerrain>();

		if (m_positionalDeviceTracker != null)
		{
			if (!m_positionalDeviceTracker.IsActive)
				m_positionalDeviceTracker.Start();

			if (m_positionalDeviceTracker.IsActive && !m_smartTerrain.IsActive)
				m_smartTerrain.Start();
		}
	}
	#endregion
	
	#region PRIVATE_METHODS
	private void DestroyAnchors()
	{
		//If running on Device
		if (!VuforiaRuntimeUtilities.IsPlayMode())
		{
			var trackableBehaviours = m_stateManager.GetActiveTrackableBehaviours();
			foreach (var behaviour in trackableBehaviours)
			{
				if (!(behaviour is AnchorBehaviour)) 
					continue;
				
				m_augmentation.transform.parent = null;

				//Only destroy anchors for the current placement mode
				if (behaviour.Trackable.Name.Contains("PlaneAnchor") && m_placementMode == EPlacementMode.Ground ||
				    behaviour.Trackable.Name.Contains("MidAirAnchor") && m_placementMode == EPlacementMode.MidAir)
				{
					m_stateManager.DestroyTrackableBehavioursForTrackable(behaviour.Trackable);
					m_stateManager.ReassociateTrackables();
				}
			}
		}
	}
	
	private void SetSurfaceIndicatorVisible(bool _isVisible)
	{
		var renderers = m_planeFinder.PlaneIndicator.GetComponentsInChildren<Renderer>(true);
		var canvas = m_planeFinder.PlaneIndicator.GetComponentsInChildren<Canvas>(true);

		foreach (var c in canvas)
			c.enabled = _isVisible;

		foreach (var r in renderers)
			r.enabled = _isVisible;
	}

	private void RotateTowardCamera(GameObject _augmentation)
	{
		var lookAtPosition = m_mainCamera.transform.position - _augmentation.transform.position;
		lookAtPosition.y = 0;
		var rotation = Quaternion.LookRotation(lookAtPosition);
		_augmentation.transform.rotation = rotation;
	}
	#endregion
	
	#region PUBLIC_METHODS
	public void HandleAutomaticHitTest(HitTestResult result)
	{
		m_autoHitTestFrameCount = Time.frameCount;
 
		if (!m_groundToggle.interactable)
		{
			// Runs only once after first successful Automatic hit test
			m_groundToggle.interactable = true;
		}
 
		if (m_placementMode == EPlacementMode.Ground && !m_augmentation.activeInHierarchy)
			SetSurfaceIndicatorVisible(false);
	}
     
	public void HandleInteractiveHitTest(HitTestResult result)
	{	
		Debug.Assert(m_placementMode == EPlacementMode.Ground);
		Debug.Assert(m_positionalDeviceTracker != null && m_positionalDeviceTracker.IsActive);
		
		if (result == null) 
			return;
		
		DestroyAnchors();
 
		m_anchorCounter++;
		m_planeAnchor = m_positionalDeviceTracker.CreatePlaneAnchor("PlaneAnchor_" + m_anchorCounter.ToString(), result);
		m_planeAnchor.name = "PlaneAnchor";
 
		if (!m_midAirToggle.interactable)
		{
			// Runs only once after first successful Ground Anchor is created
			m_midAirToggle.interactable = true;
			m_resetButton.interactable = true;
		}
 
		if (!m_augmentation.activeInHierarchy)
		{
			//The first time, unhide the augmentation
			m_augmentation.SetActive(true);
		}
 
		//Parent the augmentation to the new anchor
		m_augmentation.transform.SetParent(m_planeAnchor.transform);
		m_augmentation.transform.localPosition = Vector3.zero;
		RotateTowardCamera(m_augmentation);
	}
	
	public void PlaceObjectInMidAir(Transform midAirTransform)
	{
		Debug.Assert(m_placementMode == EPlacementMode.MidAir);
		Debug.Assert(m_positionalDeviceTracker != null && m_positionalDeviceTracker.IsActive);
		
		DestroyAnchors();

		//Create Anchor
		m_anchorCounter++;
		m_midAirAnchor = m_positionalDeviceTracker.CreateMidAirAnchor("MidAirAnchor_" + m_anchorCounter.ToString(), midAirTransform);
		m_midAirAnchor.name = "MidAirAnchor";

		if (!m_augmentation.activeInHierarchy)
		{
			//The first time, unhide the augmentation
			m_augmentation.SetActive(true);
		}

		//Parent the augmentation to the new anchor
		m_augmentation.transform.SetParent(m_midAirAnchor.transform);
		m_augmentation.transform.localPosition = Vector3.zero;
		RotateTowardCamera(m_augmentation);
	}
	#endregion
}
