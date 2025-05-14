using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

// Define operation modes
public enum OperationMode
{
    Training,
    Study
}

enum ConstructionType
{
    ActiveDrilling,
    PassiveMoving
}

public class UserStudyInterface : MonoBehaviour
{
    [Header("Experiment Details")]
    [SerializeField] private int ParticipantID = 0;
    [SerializeField] private ConstructionType constructionType = ConstructionType.ActiveDrilling;
    private float time;
    private int pressRecordingButtonTimes = 0;
    private string filePath;


    [Header("Mode Settings")]
    [SerializeField] private OperationMode currentMode = OperationMode.Study;
    [SerializeField] private int trainingBurstPointIndex = 0; // Index of burst point to use in training mode
    [SerializeField] private float trainingBurstInterval = 3f; // Time between automatic training bursts
    [SerializeField] private float trainingBurstDuration = 2f; // Duration of each training burst
    [SerializeField] private int trainingIterations = 5; // Number of burst iterations in training mode
    [SerializeField] private bool loopTrainingInfinitely = false; // Whether to loop training infinitely or use iterations

    [Header("Burst System")]
    [SerializeField] private GameObject particlePrefab;
    [SerializeField] private float burstDuration = 3f;
    [SerializeField] private List<GameObject> burstPoints = new List<GameObject>();
    [SerializeField] private GameObject drillerObject;
    [SerializeField] private bool showBurstVisualization = true; // Toggle for showing burst visualization
    [SerializeField] private float hideYOffset = -100f; // How far underground to hide visualization

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
    [SerializeField] private TMP_Text visualizationStatusText; // Added to show visualization status
    [SerializeField] private TMP_Text modeText; // Added to show current mode
    [SerializeField] private TMP_Text iterationText; // To show current training iteration

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
    private Dictionary<GameObject, Vector3> originalPositions = new Dictionary<GameObject, Vector3>(); // Store original positions
    private float currentBurstParameter;
    private bool gameCompleted = false;
    private float burstActiveTimer = 0f;
    private GameObject currentActiveBurstPoint = null; // Track which burst point is currently active
    private GameObject trainingBurstPoint = null; // Reference to the burst point used in training mode
    private Coroutine trainingCoroutine = null; // Reference to the training coroutine
    private int currentTrainingIteration = 0; // Current iteration in training mode
    private float burstTimerPassive = 0f;
    private float timer = 0f;


    void Start()
    {
        UnityEngine.Random.InitState(123);
        //TODO
        // Create a unique filename with timestamp
        // string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        filePath = Application.dataPath + "/Data_Collected/" + "/Data_PID" + ParticipantID + "_" + constructionType + ".csv";

        // Initialize CSV file with headers if it doesn't exist
        if (!File.Exists(filePath))
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("DateTime,TimeToResponse,ResponseTimes");
            }
        }

        if (constructionType == ConstructionType.PassiveMoving)
        {
            GameObject.Find("Driller").SetActive(false);
        }


        // Initialize burst systems for each burst point
        foreach (GameObject burstPoint in burstPoints)
        {
            PM25TrailBurst burst = PM25TrailBurst.Create(
                particlePrefab,
                burstPoint.transform,
                burstDuration,
                particleCount: 50,
                burstForce: 10f,
                burstSpread: 5.0f,
                startBursting: false
            );

            // Disable default mouse control - we'll handle it
            burst.useMouseControl = false;

            // Store the burst system
            burstSystems[burstPoint] = burst;

            // Store original position
            originalPositions[burstPoint] = burstPoint.transform.position;

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
        UpdateVisualizationStatusUI();
        UpdateModeUI();
        UpdateIterationUI();

        // Setup mode-specific settings
        SetupOperationMode(currentMode);

        // Start random parameter updates
        InvokeRepeating("UpdateEnvironmentParameters", 0.5f, 1.0f);

        currentWindSpeed = baseWindSpeed;
        currentHumidity = baseHumidity;
        currentTemperature = baseTemperature;
    }

    void OnDisable()
    {
        // Make sure to stop the training coroutine when disabled
        if (trainingCoroutine != null)
        {
            StopCoroutine(trainingCoroutine);
            trainingCoroutine = null;
        }
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (constructionType == ConstructionType.PassiveMoving && timer > 1.5f)
        {
            burstTimerPassive += Time.deltaTime;
            timer = 0;

            // Update PM2.5 parameter
            currentBurstParameter += (float)(Random.Range(0.1f, 1f) * (1.0 / (1.0 + 0.1 * burstTimerPassive)));
            UpdateUITexts();
            UpdateEnvironmentParameters();
        }


        if (gameCompleted)
            return;

        // Different handling based on mode
        if (currentMode == OperationMode.Study)
        {
            // Study mode - Standard interaction with hidden visualization
            HandleBurstInput();
            UpdateBurstParameter();
            CheckGameCompletion();
        }
        // Note: Training mode is handled by coroutine, no need for input handling

        // Toggle visualization with a key (for testing purposes)
        if (Input.GetKeyDown(KeyCode.V))
        {
            ToggleBurstVisualization();
        }

        // Toggle mode with M key (for testing)
        if (Input.GetKeyDown(KeyCode.M))
        {
            ToggleOperationMode();
        }

        // Restart training with R key (for testing)
        if (Input.GetKeyDown(KeyCode.R) && currentMode == OperationMode.Training)
        {
            RestartTraining();
        }

        time += Time.deltaTime;
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) || Input.GetKeyDown(KeyCode.Mouse1) && currentMode == OperationMode.Study)
        {
            pressRecordingButtonTimes++;
            Debug.Log("Recording Time: " + time + "; Pressed Times: " + pressRecordingButtonTimes);
            SaveToCSV(time, pressRecordingButtonTimes);
        }

    }

    //TODO
    private void SaveToCSV(float timeToResponse, int responseTimes)
    {
        try
        {
            // Create string with current data
            string dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string dataString = string.Format("{0},{1},{2}", dateTime, timeToResponse, responseTimes);

            // Append to CSV file
            using (StreamWriter writer = new StreamWriter(filePath, true))
            {
                writer.WriteLine(dataString);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error saving to CSV: " + e.Message);
        }
    }

    // Set up operation mode
    void SetupOperationMode(OperationMode mode)
    {
        // Stop existing coroutines if any
        if (trainingCoroutine != null)
        {
            StopCoroutine(trainingCoroutine);
            trainingCoroutine = null;
        }

        // Reset state
        foreach (var system in burstSystems.Values)
        {
            system.StopBursting();
        }
        currentActiveBurstPoint = null;

        if (mode == OperationMode.Training)
        {
            // Training mode setup

            // Ensure index is valid
            trainingBurstPointIndex = Mathf.Clamp(trainingBurstPointIndex, 0, burstPoints.Count - 1);

            // Get the training burst point
            if (burstPoints.Count > 0)
            {
                trainingBurstPoint = burstPoints[trainingBurstPointIndex];

                // Move all burst points to their original positions to make them visible
                SetBurstVisualization(true);

                // Reset iteration counter
                currentTrainingIteration = 0;
                UpdateIterationUI();

                // Start training coroutine
                trainingCoroutine = StartCoroutine(TrainingModeRoutine());
            }
            else
            {
                Debug.LogError("No burst points available for training mode!");
            }
        }
        else // Study mode
        {
            // Study mode setup
            // Hide burst visualization for study mode
            SetBurstVisualization(false);
            trainingBurstPoint = null;
        }

        UpdateModeUI();
    }

    // Restart the training cycle
    public void RestartTraining()
    {
        if (currentMode == OperationMode.Training)
        {
            // Stop existing coroutine
            if (trainingCoroutine != null)
            {
                StopCoroutine(trainingCoroutine);
                trainingCoroutine = null;
            }

            // Reset state
            foreach (var system in burstSystems.Values)
            {
                system.StopBursting();
            }

            // Reset iteration counter
            currentTrainingIteration = 0;
            UpdateIterationUI();

            // Start training coroutine again
            trainingCoroutine = StartCoroutine(TrainingModeRoutine());

            Debug.Log("Training restarted");
        }
    }

    // Training mode coroutine - automatically triggers burst at the training point
    IEnumerator TrainingModeRoutine()
    {
        // Continue until we complete all iterations or exit training mode
        while ((loopTrainingInfinitely || currentTrainingIteration < trainingIterations) &&
               currentMode == OperationMode.Training &&
               trainingBurstPoint != null)
        {
            // Update iteration counter if not looping infinitely
            if (!loopTrainingInfinitely)
            {
                currentTrainingIteration++;
                UpdateIterationUI();

                // Check if we've reached the maximum iterations
                if (currentTrainingIteration > trainingIterations)
                {
                    Debug.Log("Training completed all iterations!");
                    yield break; // Exit the coroutine
                }
            }

            // Start the burst effect
            if (burstSystems.TryGetValue(trainingBurstPoint, out PM25TrailBurst burstSystem))
            {
                // Trigger burst
                burstSystem.StartBursting();

                // Update PM2.5 values while burst is active
                float burstTimer = 0f;
                while (burstTimer < trainingBurstDuration)
                {
                    burstTimer += Time.deltaTime;
                    burstActiveTimer += Time.deltaTime;

                    // Update PM2.5 parameter
                    currentBurstParameter += (float)(0.028 * (1.0 / (1.0 + 0.1 * burstActiveTimer)));
                    UpdateUITexts();

                    yield return null;
                }

                // Stop the burst
                burstSystem.StopBursting();
            }

            // Wait before the next burst
            yield return new WaitForSeconds(trainingBurstInterval);
        }

        Debug.Log("Training sequence completed!");
    }

    // Update the UI to show current iteration
    void UpdateIterationUI()
    {
        if (iterationText != null)
        {
            if (loopTrainingInfinitely)
            {
                iterationText.text = "Training: Continuous Mode";
            }
            else
            {
                iterationText.text = $"Training: {currentTrainingIteration}/{trainingIterations}";
            }
        }
    }

    // Toggle between Training and Study modes
    public void ToggleOperationMode()
    {
        currentMode = (currentMode == OperationMode.Training) ?
                      OperationMode.Study : OperationMode.Training;

        SetupOperationMode(currentMode);
        Debug.Log($"Switched to {currentMode} mode");
    }

    // These methods handle actual collider-based collision detection
    void OnTriggerEnter(Collider other)
    {
        // Only process collisions in Study mode
        if (currentMode == OperationMode.Study)
        {
            // Check if the collision is between driller and a burst point
            CheckCollisionWithBurstPoint(other.gameObject, true);
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Only process collisions in Study mode
        if (currentMode == OperationMode.Study)
        {
            // Check if the collision ended between driller and a burst point
            CheckCollisionWithBurstPoint(other.gameObject, false);
        }
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

    // Only used in Study mode
    void HandleBurstInput()
    {
        bool isButtonPressed = Input.GetKey(burstKey) || OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger) > 0.5f;

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
            // But in Study mode, visualization is hidden, so this mostly affects PM2.5 calculations
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

            // Update PM2.5 parameter based on burst time
            currentBurstParameter += (float)(0.01 * (1.0 / (1.0 + 0.1 * burstActiveTimer)));

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
            currentTemperature += Random.Range(-0.01f, 0.01f);

            // Ensure values don't drift too far from base values (optional)
            currentWindSpeed = Mathf.Clamp(currentWindSpeed, baseWindSpeed - 0.3f, baseWindSpeed + 0.3f);
            currentHumidity = Mathf.Clamp(currentHumidity, baseHumidity - 0.1f, baseHumidity + 0.1f);
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

    // Update the UI to show current visualization status
    void UpdateVisualizationStatusUI()
    {
        if (visualizationStatusText != null)
        {
            visualizationStatusText.text = $"PM2.5 Visualization: {(showBurstVisualization ? "ON" : "OFF")}";
        }
    }

    // Update the UI to show current mode
    void UpdateModeUI()
    {
        if (modeText != null)
        {
            modeText.text = $"Mode: {currentMode}";
        }
    }

    void CheckGameCompletion()
    {
        // Check if all burst points are completed
        bool allCompleted = true;
        foreach (var objectComplete in burstPointCompleted)
        {
            if (objectComplete.Value == true && GameObject.Find("PM25TrailBurst_" + objectComplete.Key.name) != null)
                GameObject.Find("PM25TrailBurst_" + objectComplete.Key.name).SetActive(false);

            if (!objectComplete.Value)
                allCompleted = false;
        }

        // // If all are completed and game isn't already marked as completed
        //TODO Stop Game;
        if (allCompleted)
        {
            // gameCompleted = true;

            Debug.Log("All burst points completed! Game over.");
            StopPlayMode();
        }
    }

    // Set the visualization state (show or hide particles by moving them underground)
    public void SetBurstVisualization(bool isVisible)
    {
        showBurstVisualization = isVisible;

        foreach (var entry in burstSystems)
        {
            GameObject burstPoint = entry.Key;
            PM25TrailBurst burstSystem = entry.Value;

            // Get the original position or default to current position if not stored
            Vector3 originalPos = originalPositions.ContainsKey(burstPoint)
                ? originalPositions[burstPoint]
                : burstPoint.transform.position;

            if (isVisible)
            {
                // Restore to original position
                burstSystem.transform.position = new Vector3(0.01f, 0.01f, 0.01f);
            }
            else
            {
                // Move underground by adjusting Y position
                // Vector3 hiddenPos = originalPos;
                // hiddenPos.y += hideYOffset; // Move down underground
                burstSystem.transform.localScale = new Vector3(0.0f, 0.0f, 0.0f);
            }
        }

        UpdateVisualizationStatusUI();
        Debug.Log($"PM2.5 visualization is now {(showBurstVisualization ? "ON" : "OFF")}");
    }

    // Toggle the visualization state
    public void ToggleBurstVisualization()
    {
        SetBurstVisualization(!showBurstVisualization);
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
        private UserStudyInterface parent;
        private GameObject burstPoint;

        public void Initialize(UserStudyInterface parent, GameObject burstPoint)
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

    private void StopPlayMode()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Debug.Log("The condition has been finished.");
#endif
    }
}