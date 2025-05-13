using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;


public class BurstControl : MonoBehaviour
{
    [Header("Burst System")]
    [SerializeField] private GameObject particlePrefab;
    [SerializeField] private float burstDuration = 3f;
    [SerializeField] private List<GameObject> burstPoints = new List<GameObject>();
    [SerializeField] private GameObject drillerObject;

    [Header("Control Settings")]
    [SerializeField] private KeyCode burstKey = KeyCode.Mouse0;
    [SerializeField] private float requiredHoldTime = 3.0f;
    [SerializeField] private float touchingDistance = 0.5f;
    [SerializeField] private bool touchingRequired = true; // Whether touching is required to burst

    [Header("UI Elements")]
    [SerializeField] private TMP_Text burstParameterText;
    [SerializeField] private TMP_Text windSpeedText;
    [SerializeField] private TMP_Text humidityText;
    [SerializeField] private TMP_Text temperatureText;
    [SerializeField] private TMP_Text touchingStatusText; // Added to show current touching status

    [Header("Environment Parameters")]
    [SerializeField] private float baseWindSpeed = 0.2f;
    [SerializeField] private float baseHumidity = 60.0f;
    [SerializeField] private float baseTemperature = 25.0f;
    [SerializeField] private float parameterVariation = 1f; // % variation around base value
    [SerializeField] private bool useVerySubtleChanges = true; // Flag to control change magnitude

    // Current values
    private float currentWindSpeed;
    private float currentHumidity;
    private float currentTemperature;

    [Header("Burst Parameter Settings")]
    [SerializeField] private float initialBurstParameter = 1.0f;
    [SerializeField] private float[] burstParameterThresholds = { 1.0f, 2.0f }; // Time thresholds in seconds
    [SerializeField] private float[] burstParameterValues = { 1.5f, 3.2f }; // Corresponding values

    [Header("Visual Feedback")]
    [SerializeField] private Color normalColor = Color.red;
    [SerializeField] private Color completedColor = Color.green;

    // Private variables
    private Dictionary<GameObject, PM25TrailBurst> burstSystems = new Dictionary<GameObject, PM25TrailBurst>();
    private Dictionary<GameObject, bool> burstPointCompleted = new Dictionary<GameObject, bool>();
    private Dictionary<GameObject, float> burstPointHoldTimes = new Dictionary<GameObject, float>();
    private Dictionary<GameObject, bool> burstPointsTouching = new Dictionary<GameObject, bool>();
    private Dictionary<GameObject, int> burstPointCollisionCount = new Dictionary<GameObject, int>(); // Track collisions
    private float currentBurstParameter;
    private bool gameCompleted = false;
    private float burstActiveTimer = 0f;
    private GameObject currentActiveBurstPoint = null; // Track which burst point is currently active

    void Start()
    {
        // Initialize burst systems for each burst point
        foreach (GameObject burstPoint in burstPoints)
        {
            PM25TrailBurst burst = PM25TrailBurst.Create(
                particlePrefab,
                burstPoint.transform,
                burstDuration,
                particleCount: 500,
                burstForce: 10f,
                burstSpread: 5.0f,
                startBursting: false
            );

            // Disable default mouse control - we'll handle it
            burst.useMouseControl = false;

            // Store the burst system
            burstSystems[burstPoint] = burst;

            // Initialize tracking dictionaries
            burstPointCompleted[burstPoint] = false;
            burstPointHoldTimes[burstPoint] = 0f;
            burstPointsTouching[burstPoint] = false;
            burstPointCollisionCount[burstPoint] = 0;

            // Ensure each burst point has a collider marked as trigger
            SphereCollider collider = burstPoint.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = burstPoint.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                // Set radius based on the burst point's scale
                collider.radius = 0.5f; // Adjust if needed
            }

            // Set initial color
            Renderer renderer = burstPoint.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = normalColor;
            }
        }

        // Ensure driller has a collider
        if (drillerObject != null && drillerObject.GetComponent<Collider>() == null)
        {
            drillerObject.AddComponent<BoxCollider>();
            Debug.Log("Added BoxCollider to driller object.");
        }

        // Initialize UI values
        currentBurstParameter = initialBurstParameter;
        UpdateUITexts();
        UpdateTouchingRequirementUI();

        // Start random parameter updates
        InvokeRepeating("UpdateEnvironmentParameters", 0.5f, 1.0f);

        currentWindSpeed = baseWindSpeed;
        currentHumidity = baseHumidity;
        currentTemperature = baseTemperature;
    }

    void Update()
    {
        if (gameCompleted)
            return;

        // Handle input and bursting
        HandleBurstInput();

        // Update burst parameter if actively bursting
        UpdateBurstParameter();

        // Check if game is completed
        CheckGameCompletion();
    }

    // These methods handle actual collider-based collision detection
    void OnTriggerEnter(Collider other)
    {
        // Check if the collision is between driller and a burst point
        CheckCollisionWithBurstPoint(other.gameObject, true);
    }

    void OnTriggerExit(Collider other)
    {
        // Check if the collision ended between driller and a burst point
        CheckCollisionWithBurstPoint(other.gameObject, false);
    }

    // Add these methods to the burst points to detect collisions
    void OnEnable()
    {
        // Add trigger callbacks to all burst points
        foreach (GameObject burstPoint in burstPoints)
        {
            // Only add the handler if it doesn't already exist
            if (burstPoint.GetComponent<BurstPointCollisionHandler>() == null)
            {
                burstPoint.AddComponent<BurstPointCollisionHandler>().Initialize(this, burstPoint);
            }
        }

        // Ensure driller has a collider
        if (drillerObject != null && drillerObject.GetComponent<Collider>() == null)
        {
            BoxCollider collider = drillerObject.AddComponent<BoxCollider>();
            collider.isTrigger = false; // Not a trigger, a solid collider
            Debug.Log("Added BoxCollider to driller object.");
        }

        // Reset collision states
        foreach (GameObject burstPoint in burstPoints)
        {
            burstPointCollisionCount[burstPoint] = 0;
            burstPointsTouching[burstPoint] = false;
        }
    }

    // Handles collision events from burst points
    public void OnBurstPointCollisionEnter(GameObject burstPoint)
    {
        if (burstPoint == null || !burstPoints.Contains(burstPoint)) return;

        int count = burstPointCollisionCount.ContainsKey(burstPoint) ? burstPointCollisionCount[burstPoint] : 0;
        burstPointCollisionCount[burstPoint] = count + 1;
        burstPointsTouching[burstPoint] = true;
        Debug.Log($"Driller entered collision with {burstPoint.name}, count: {burstPointCollisionCount[burstPoint]}");
    }

    public void OnBurstPointCollisionExit(GameObject burstPoint)
    {
        if (burstPoint == null || !burstPoints.Contains(burstPoint)) return;

        int count = burstPointCollisionCount.ContainsKey(burstPoint) ? burstPointCollisionCount[burstPoint] : 0;
        burstPointCollisionCount[burstPoint] = Mathf.Max(0, count - 1);
        burstPointsTouching[burstPoint] = burstPointCollisionCount[burstPoint] > 0;
        Debug.Log($"Driller exited collision with {burstPoint.name}, count: {burstPointCollisionCount[burstPoint]}");

        // If this was the active burst point, stop bursting and reset progress
        if (currentActiveBurstPoint == burstPoint && !burstPointsTouching[burstPoint])
        {
            // Only if this point wasn't already completed
            if (!burstPointCompleted[burstPoint])
            {
                burstSystems[burstPoint].StopBursting();
                burstPointHoldTimes[burstPoint] = 0f;
            }

            // Reset active point when collision ends
            currentActiveBurstPoint = null;
        }
    }

    private void CheckCollisionWithBurstPoint(GameObject collidedObject, bool isEntering)
    {
        // Check if this object is one of our burst points
        foreach (GameObject burstPoint in burstPoints)
        {
            if (collidedObject == burstPoint)
            {
                if (isEntering)
                {
                    OnBurstPointCollisionEnter(burstPoint);
                }
                else
                {
                    OnBurstPointCollisionExit(burstPoint);
                }
                return;
            }
        }
    }

    void HandleBurstInput()
    {
        bool isButtonPressed = Input.GetKey(burstKey);

        // ALWAYS stop ALL burst systems first - this ensures bursts ONLY happen when 
        // we explicitly tell them to start below (and never without the button pressed)
        foreach (var burstSystem in burstSystems.Values)
        {
            burstSystem.StopBursting();
        }

        // If button is not pressed, reset progress but don't burst
        if (!isButtonPressed)
        {
            // Reset all hold times when button is released
            foreach (GameObject burstPoint in burstPoints)
            {
                if (!burstPointCompleted[burstPoint])
                {
                    burstPointHoldTimes[burstPoint] = 0f;
                }
            }

            // Reset active point
            currentActiveBurstPoint = null;
            return;
        }

        // Button IS pressed - handle bursting logic

        // If active burst point is no longer colliding, find a new one
        if (currentActiveBurstPoint != null && !burstPointsTouching[currentActiveBurstPoint])
        {
            currentActiveBurstPoint = null;
        }

        // If we don't have an active burst point, find a new one that's colliding
        // This time we'll consider both completed and uncompleted points
        if (currentActiveBurstPoint == null)
        {
            // Find first burst point that's currently colliding
            foreach (GameObject burstPoint in burstPoints)
            {
                // Check if this burst point is colliding
                if (burstPointsTouching[burstPoint])
                {
                    currentActiveBurstPoint = burstPoint;
                    Debug.Log($"Selected new active burst point: {burstPoint.name} (Completed: {burstPointCompleted[burstPoint]})");
                    break;
                }
            }
        }

        // Now if we have an active point, process it
        if (currentActiveBurstPoint != null && burstPointsTouching[currentActiveBurstPoint])
        {
            // Start bursting for the active point regardless of completion status
            burstSystems[currentActiveBurstPoint].StartBursting();

            // Only update hold time if point is not completed yet
            if (!burstPointCompleted[currentActiveBurstPoint])
            {
                burstPointHoldTimes[currentActiveBurstPoint] += Time.deltaTime;

                // Check if just completed
                if (burstPointHoldTimes[currentActiveBurstPoint] >= requiredHoldTime)
                {
                    // Mark as completed
                    burstPointCompleted[currentActiveBurstPoint] = true;

                    // Change color
                    Renderer renderer = currentActiveBurstPoint.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = completedColor;
                    }

                    Debug.Log($"Burst point completed: {currentActiveBurstPoint.name}");

                    // Check if this was the last point
                    CheckGameCompletion();

                    // Note: We no longer stop bursting or reset active point when completed
                    // The burst will continue as long as button is pressed and driller is colliding
                }
            }
        }
    }

    void UpdateBurstParameter()
    {
        // Check if any burst system is actively emitting
        bool anyBursting = false;
        foreach (var system in burstSystems.Values)
        {
            if (system.IsEmitting())
            {
                anyBursting = true;
                break;
            }
        }

        // Update timer if actively bursting
        if (anyBursting)
        {
            burstActiveTimer += Time.deltaTime;

            // Update parameter based on burst time
            // for (int i = 0; i < burstParameterThresholds.Length; i++)
            // {
            //     if (burstActiveTimer >= burstParameterThresholds[i])
            //     {
            //         currentBurstParameter = burstParameterValues[i];
            //     }
            // }
            currentBurstParameter += (float)(0.013 * (1.0 / (1.0 + 0.1 * burstActiveTimer)));

            // Update UI
            UpdateUITexts();
        }
        else
        {
            // Reset timer when not bursting
            burstActiveTimer = 0f;
        }
    }

    void UpdateEnvironmentParameters()
    {
        if (useVerySubtleChanges)
        {
            // Apply very subtle changes (0.01 level)
            currentWindSpeed += Random.Range(-0.01f, 0.01f);
            currentHumidity += Random.Range(-0.1f, 0.1f);
            currentTemperature += Random.Range(-0.1f, 0.1f);

            // Ensure values don't drift too far from base values (optional)
            currentWindSpeed = Mathf.Clamp(currentWindSpeed, baseWindSpeed - 0.2f, baseWindSpeed + 0.2f);
            currentHumidity = Mathf.Clamp(currentHumidity, baseHumidity - 0.2f, baseHumidity + 0.2f);
            currentTemperature = Mathf.Clamp(currentTemperature, baseTemperature - 0.2f, baseTemperature + 0.2f);
        }
        else
        {
            // Original implementation with more noticeable changes
            float windVariation = Random.Range(-parameterVariation, parameterVariation) * baseWindSpeed;
            float humidityVariation = Random.Range(-parameterVariation, parameterVariation) * baseHumidity;
            float temperatureVariation = Random.Range(-parameterVariation, parameterVariation) * baseTemperature;

            currentWindSpeed = baseWindSpeed + windVariation;
            currentHumidity = baseHumidity + humidityVariation;
            currentTemperature = baseTemperature + temperatureVariation;
        }

        // Apply to UI with 2 decimal places for more precision
        if (windSpeedText != null)
            windSpeedText.text = $"Wind Speed: {math.abs(currentWindSpeed):F2} m/s";

        if (humidityText != null)
            humidityText.text = $"Humidity: {currentHumidity:F1}%";

        if (temperatureText != null)
            temperatureText.text = $"Temperature: {currentTemperature:F1}°C";
    }

    void UpdateUITexts()
    {
        // Update burst parameter text
        if (burstParameterText != null)
        {
            burstParameterText.text = $"PM2.5: {currentBurstParameter:F1} μg/m³";
        }
    }

    // Update the UI to show current touching requirement status
    void UpdateTouchingRequirementUI()
    {
        if (touchingStatusText != null)
        {
            touchingStatusText.text = $"Touch Required: {(touchingRequired ? "ON" : "OFF")}";
        }
    }

    void CheckGameCompletion()
    {
        // // Check if all burst points are completed
        // bool allCompleted = true;
        // foreach (bool completed in burstPointCompleted.Values)
        // {
        //     if (!completed)
        //     {
        //         allCompleted = false;
        //         break;
        //     }
        // }

        // If all are completed and game isn't already marked as completed
        // if (allCompleted && !gameCompleted)
        // {
        //     gameCompleted = true;
        //     Debug.Log("All burst points completed! Game over.");

        //     // Don't automatically stop bursting - let the button control continue to work
        //     // This allows the player to continue bursting at completed points if desired
        // }
    }

    public void ResetAllBurstPoints()
    {
        foreach (GameObject burstPoint in burstPoints)
        {
            burstPointCompleted[burstPoint] = false;
            burstPointHoldTimes[burstPoint] = 0f;
            burstPointCollisionCount[burstPoint] = 0;
            burstPointsTouching[burstPoint] = false;

            // Stop bursting
            burstSystems[burstPoint].StopBursting();

            // Reset color
            Renderer renderer = burstPoint.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = normalColor;
            }
        }

        // Reset game state
        gameCompleted = false;
        burstActiveTimer = 0f;
        currentBurstParameter = initialBurstParameter;
        currentActiveBurstPoint = null; // Reset the active burst point
        UpdateUITexts();

        Debug.Log("All burst points have been reset.");
    }

    // Toggle whether touching is required to burst
    public void ToggleTouchingRequirement()
    {
        // Note: This function is now just for UI purposes as collision is always required
        // based on the specifications, but we keep it for backward compatibility
        touchingRequired = true; // Always true based on the requirement
        UpdateTouchingRequirementUI();

        // Stop all bursting when changing the mode
        foreach (var system in burstSystems.Values)
        {
            system.StopBursting();
        }

        // Reset hold times when changing mode
        foreach (GameObject burstPoint in burstPoints)
        {
            burstPointHoldTimes[burstPoint] = 0f;
        }

        // Reset active burst point when changing mode
        currentActiveBurstPoint = null;

        Debug.Log("Collision detection is required for bursting.");
    }

    // Add this helper class to handle collisions for each burst point
    public class BurstPointCollisionHandler : MonoBehaviour
    {
        private BurstControl parent;
        private GameObject burstPoint;

        public void Initialize(BurstControl parent, GameObject burstPoint)
        {
            this.parent = parent;
            this.burstPoint = burstPoint;

            // Ensure this burst point has a trigger collider
            SphereCollider collider = burstPoint.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = burstPoint.AddComponent<SphereCollider>();
                Debug.Log($"Added SphereCollider to {burstPoint.name}");
            }
            collider.isTrigger = true;
            collider.radius = 0.5f; // Adjust based on your objects
        }

        void OnTriggerEnter(Collider other)
        {
            // Check if the collision is with the driller
            if (parent != null && parent.drillerObject != null && other.gameObject == parent.drillerObject)
            {
                Debug.Log($"Driller entered {burstPoint.name}");
                parent.OnBurstPointCollisionEnter(burstPoint);
            }
        }

        void OnTriggerExit(Collider other)
        {
            // Check if the collision ended with the driller
            if (parent != null && parent.drillerObject != null && other.gameObject == parent.drillerObject)
            {
                Debug.Log($"Driller exited {burstPoint.name}");
                parent.OnBurstPointCollisionExit(burstPoint);
            }
        }
    }
}