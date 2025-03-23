using UnityEngine;

public class AutoCueChop : MonoBehaviour
{
    // Chopping settings
    public float chopAngle = 45f;        // Maximum chop angle
    public float chopSpeed = 60f;        // Degrees per second
    public float pauseTime = 0.5f;       // Pause at top and bottom of motion

    // Animation state
    private bool isChopping = true;      // Start in chopping state
    private bool isReturning = false;
    private float currentAngle = 0f;
    private float pauseTimer = 0f;
    private Vector3 pivotPoint;
    private Quaternion startRotation;

    // Visualization
    public bool showPivotPoint = true;
    private GameObject pivotVisual;

    void Start()
    {
        // Store the initial rotation
        startRotation = transform.rotation;

        // For a cue stick with dimensions x:0.3, y:0.01, z:0.01
        // Calculate the pivot point at one end of the cue stick
        // Assuming the pivot should be at the "grip" end (negative X)
        pivotPoint = transform.position - new Vector3(0.15f, 0, 0);

        // Visualize the pivot point if enabled
        if (showPivotPoint)
        {
            pivotVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pivotVisual.transform.position = pivotPoint;
            pivotVisual.transform.localScale = Vector3.one * 0.02f;

            // Change the sphere color
            Renderer pivotRenderer = pivotVisual.GetComponent<Renderer>();
            if (pivotRenderer != null)
            {
                pivotRenderer.material.color = Color.red;
            }

            // Remove collider from visual
            Collider collider = pivotVisual.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }
    }

    void Update()
    {
        // If we're pausing, handle the timer
        if (pauseTimer > 0)
        {
            pauseTimer -= Time.deltaTime;
            return; // Skip the rest of the update
        }

        // Handle automatic chopping
        if (isChopping)
        {
            // Increase angle
            currentAngle += chopSpeed * Time.deltaTime;

            // Apply rotation around pivot point
            RotateAroundPivot(currentAngle);

            // Check if we've reached the target angle
            if (currentAngle >= chopAngle)
            {
                isChopping = false;
                isReturning = true;
                pauseTimer = pauseTime; // Pause at the top
            }
        }

        // Handle automatic return
        if (isReturning)
        {
            // Decrease angle
            currentAngle -= chopSpeed * Time.deltaTime;

            // Apply rotation around pivot point
            RotateAroundPivot(currentAngle);

            // Check if we've returned to the starting position
            if (currentAngle <= 0)
            {
                isReturning = false;
                isChopping = true;
                transform.rotation = startRotation; // Ensure exact return
                pauseTimer = pauseTime; // Pause at the bottom
            }
        }
    }

    void RotateAroundPivot(float angle)
    {
        // Reset to start rotation first
        transform.rotation = startRotation;

        // Then rotate around pivot point on the z-axis (appropriate for a cue stick)
        transform.RotateAround(pivotPoint, Vector3.forward, angle);
    }

    // Clean up the visualization when the script is disabled or destroyed
    void OnDisable()
    {
        if (pivotVisual != null)
        {
            Destroy(pivotVisual);
        }
    }
}