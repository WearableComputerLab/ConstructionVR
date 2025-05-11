using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PM25TrailBurst : MonoBehaviour
{
    [Header("Control Settings")]
    public bool startOnAwake = false;
    public bool useMouseControl = true;
    public KeyCode burstKey = KeyCode.Mouse0; // Default to left mouse button
    public bool isBursting = false;  // Flag to track if bursting is active

    [Header("Particle Settings")]
    public GameObject particlePrefab;
    public int particleCount = 500;
    public float particleSize = 0.01f;

    [Header("Emission Settings")]
    public Transform drillingPoint;
    public float emissionDuration = 2f;
    private float emissionTimer;
    public float emissionRadius = 0.01f;  // Radius around drilling point for emission

    [Header("Burst Settings")]
    public float burstForce = 3.0f;      // Increased upward force
    public float burstSpread = 1.5f;     // Horizontal spread factor
    public Vector3 windVelocity = new Vector3(0.2f, 0, 0);
    public float diffusionCoefficient = 0.15f;
    public float gravityStrength = 0.2f;
    public float movementSpeedMultiplier = 0.04f;

    [Header("Density-Based Color Settings")]
    public Color lowDensityColor = new Color(1, 1, 1, 0.2f);      // Light color for low density
    public Color highDensityColor = new Color(1, 1, 1, 0.2f);     // Dark color for high density
    public float densityCheckRadius = 0.05f;                    // Radius to check for neighboring particles
    public int maxDensity = 15;                                // Density value that maps to the darkest color
    public float colorPower = 0.7f;                            // Adjusts the color curve (lower = more pronounced)

    [Header("Trail Settings")]
    public float trailDuration = 0f;
    public int trailResolution = 30;
    public float trailWidth = 0.001f;
    public Gradient trailColorGradient;
    public bool useDetailedTrails = true;
    [Range(0.001f, 0.05f)]
    public float minVertexDistance = 0.005f;

    private List<ParticleData> particles = new List<ParticleData>();

    class ParticleData
    {
        public GameObject obj;
        public Vector3 velocity;
        public TrailRenderer trail;
        public float creationTime;
    }

    // Static factory method to create a new PM25TrailBurst instance
    public static PM25TrailBurst Create(
        GameObject particlePrefab,
        Transform drillingPoint,
        float emissionDuration = 2f,
        int particleCount = 500,
        float burstForce = 3.0f,
        float burstSpread = 1.5f,
        bool startBursting = false
        )
    {
        // Create a new GameObject to hold our component
        GameObject burstObj = new GameObject("PM25TrailBurst");

        // Add our component
        PM25TrailBurst burst = burstObj.AddComponent<PM25TrailBurst>();

        // Set required properties
        burst.particlePrefab = particlePrefab;
        burst.drillingPoint = drillingPoint;
        burst.emissionDuration = emissionDuration;
        burst.particleCount = particleCount;
        burst.burstForce = burstForce;
        burst.burstSpread = burstSpread;
        burst.startOnAwake = startBursting;


        // Initialize the timer
        burst.emissionTimer = emissionDuration;

        return burst;
    }

    // Regular initialization 
    void Awake()
    {
        // Initialize the trail color gradient if none was provided
        InitializeGradient();

        // Set bursting state based on startOnAwake
        isBursting = startOnAwake;
    }

    // Start is called before the first frame update
    void Start()
    {
        emissionTimer = emissionDuration;
        InvokeRepeating("UpdateParticleColors", 0.1f, 0.2f); // Update colors frequently

        // Set physics timestep to be smaller when detailed trails are used
        if (useDetailedTrails)
        {
            Time.fixedDeltaTime = 0.01f;
        }
    }

    // Initialize default gradient if none is provided
    private void InitializeGradient()
    {
        // Check if the gradient itself is null first
        if (trailColorGradient == null)
        {
            trailColorGradient = new Gradient();
        }

        // Then check if it has any color keys defined
        if (trailColorGradient.colorKeys == null || trailColorGradient.colorKeys.Length == 0)
        {
            GradientColorKey[] colorKeys = new GradientColorKey[2];
            colorKeys[0].color = highDensityColor;
            colorKeys[0].time = 0f;
            colorKeys[1].color = lowDensityColor;
            colorKeys[1].time = 1f;

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0].alpha = 0.9f;
            alphaKeys[0].time = 0f;
            alphaKeys[1].alpha = 0.4f;
            alphaKeys[1].time = 1f;

            trailColorGradient.SetKeys(colorKeys, alphaKeys);
        }
    }

    void Update()
    {
        // Check for mouse input if mouse control is enabled
        if (useMouseControl)
        {
            if (Input.GetKeyDown(burstKey))
            {
                StartBursting();
            }
            else if (Input.GetKeyUp(burstKey))
            {
                StopBursting();
            }
        }

        float dt = Time.deltaTime;

        // Only emit particles if bursting is active and emission time remains
        if (isBursting && emissionTimer > 0)
        {
            EmitParticles();
            emissionTimer -= dt;
        }

        // Continue updating existing particles regardless of bursting state
        UpdateParticles(dt);
    }

    // Update particle positions and properties
    void UpdateParticles(float dt)
    {
        // Use a smaller timestep for more accurate physics and detailed trails
        float subDt = dt / (useDetailedTrails ? 3 : 1);
        int steps = useDetailedTrails ? 3 : 1;

        foreach (ParticleData particle in particles)
        {
            if (particle.obj == null) continue;

            for (int i = 0; i < steps; i++)
            {
                // Physics-driven convection-diffusion equation
                Vector3 convectionVelocity = particle.velocity + windVelocity + Vector3.down * gravityStrength;
                Vector3 convectionStep = convectionVelocity * subDt;

                float diffusionScale = Mathf.Sqrt(2 * diffusionCoefficient * subDt);
                // Safety check to avoid potential NaN values if subDt is extremely small
                if (float.IsNaN(diffusionScale) || diffusionScale <= 0)
                {
                    diffusionScale = 0.001f; // Use small positive value instead
                }

                Vector3 diffusionStep = new Vector3(
                    NormalRandom() * diffusionScale,
                    NormalRandom() * diffusionScale,
                    NormalRandom() * diffusionScale
                );

                Vector3 gravityStep = Vector3.down * gravityStrength * subDt;

                // Apply movement in smaller steps for smoother trails
                Vector3 positionDelta = (convectionStep + diffusionStep + gravityStep) * movementSpeedMultiplier;

                // Validate position delta to prevent infinity/NaN values
                if (float.IsInfinity(positionDelta.x) || float.IsNaN(positionDelta.x) ||
                    float.IsInfinity(positionDelta.y) || float.IsNaN(positionDelta.y) ||
                    float.IsInfinity(positionDelta.z) || float.IsNaN(positionDelta.z))
                {
                    // Skip this update if values are invalid
                    continue;
                }

                // Clamp position delta to reasonable bounds to prevent extreme values
                positionDelta.x = Mathf.Clamp(positionDelta.x, -10f, 10f);
                positionDelta.y = Mathf.Clamp(positionDelta.y, -10f, 10f);
                positionDelta.z = Mathf.Clamp(positionDelta.z, -10f, 10f);

                particle.obj.transform.position += positionDelta;

                // Gradually reduce particle upward velocity (simulate burst slowing)
                particle.velocity = Vector3.Lerp(particle.velocity, Vector3.zero, 0.5f * subDt);

                // Validate velocity to prevent NaN or infinity
                if (float.IsNaN(particle.velocity.x) || float.IsInfinity(particle.velocity.x) ||
                    float.IsNaN(particle.velocity.y) || float.IsInfinity(particle.velocity.y) ||
                    float.IsNaN(particle.velocity.z) || float.IsInfinity(particle.velocity.z))
                {
                    particle.velocity = Vector3.zero;
                }
            }

            // Ensure trails remain visible throughout the particle lifetime
            if (particle.trail != null)
            {
                // Keep trail duration constant
                particle.trail.time = trailDuration;

                // Make sure trail renderer is always emitting
                if (!particle.trail.emitting)
                {
                    particle.trail.emitting = true;
                }
            }
        }
    }

    // Public methods to control bursting from other scripts

    // Start the bursting process
    public void StartBursting()
    {
        if (!isBursting)
        {
            isBursting = true;
            // Reset the timer if it has run out
            if (emissionTimer <= 0)
            {
                ResetEmissionTimer();
            }
        }
    }

    // Stop the bursting process
    public void StopBursting()
    {
        isBursting = false;
    }

    // Reset the emission timer to allow for a new full burst
    public void ResetEmissionTimer()
    {
        emissionTimer = emissionDuration;
    }

    // Set a new emission duration and reset the timer
    public void SetEmissionDuration(float duration)
    {
        emissionDuration = duration;
        ResetEmissionTimer();
    }

    // Check if particles are currently being emitted
    public bool IsEmitting()
    {
        return isBursting && emissionTimer > 0;
    }

    // Get remaining emission time
    public float GetRemainingEmissionTime()
    {
        return emissionTimer;
    }

    void EmitParticles()
    {
        int particlesToEmit = Mathf.CeilToInt(particleCount / emissionDuration * Time.deltaTime);
        for (int i = 0; i < particlesToEmit; i++)
        {
            // Emit from random position within sphere
            Vector3 emissionOffset = Random.insideUnitSphere * emissionRadius;
            Vector3 emissionPosition = drillingPoint.position + emissionOffset;

            GameObject particleObj = Instantiate(particlePrefab, emissionPosition, Quaternion.identity, transform);
            particleObj.transform.localScale = Vector3.one * particleSize;

            // Create a much more varied burst direction
            // This is the key to making particles spread over a larger area
            Vector3 initialVelocity = new Vector3(
                Random.Range(-burstSpread, burstSpread),
                burstForce + Random.Range(0f, 1f),  // Always some upward force plus random
                Random.Range(-burstSpread, burstSpread)
            );

            // Add trail renderer to particle
            TrailRenderer trail = particleObj.AddComponent<TrailRenderer>();
            ConfigureTrailRenderer(trail);

            ParticleData pd = new ParticleData
            {
                obj = particleObj,
                velocity = initialVelocity,
                trail = trail,
                creationTime = Time.time
            };

            particles.Add(pd);
        }
    }

    void ConfigureTrailRenderer(TrailRenderer trail)
    {
        trail.time = trailDuration;

        // Use finer vertex placement for detailed trails
        if (useDetailedTrails)
        {
            trail.minVertexDistance = minVertexDistance; // Very small distance for detailed capture
        }
        else
        {
            trail.minVertexDistance = trailDuration / trailResolution;
        }

        trail.widthMultiplier = trailWidth;
        trail.colorGradient = trailColorGradient;
        trail.generateLightingData = false;
        trail.autodestruct = false;
        trail.emitting = true;
        trail.textureMode = LineTextureMode.Tile; // Ensures smooth trails

        // Create a material that makes trails more visible
        Material trailMat = new Material(Shader.Find("Sprites/Default"));
        trailMat.renderQueue = 3000; // Ensure trails render on top

        // Make sure trails are visible by disabling depth testing if needed
        trailMat.SetInt("_ZWrite", 0); // Don't write to depth buffer
        trailMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always); // Always pass depth test

        trail.material = trailMat;
    }

    void UpdateParticleColors()
    {
        foreach (ParticleData particle in particles)
        {
            if (particle.obj == null) continue;

            // Simple density check - just count all colliders
            Collider[] neighbors = Physics.OverlapSphere(particle.obj.transform.position, densityCheckRadius);
            int density = neighbors.Length;

            // Map density to color (clamped between 0-1)
            float t = Mathf.Clamp01((float)density / maxDensity);

            // Make the color mapping more pronounced using the colorPower parameter
            t = Mathf.Pow(t, colorPower);

            Color currentColor = Color.Lerp(lowDensityColor, highDensityColor, t);

            // Apply color to particle
            Renderer rend = particle.obj.GetComponent<Renderer>();
            if (rend != null && rend.material != null)
            {
                rend.material.color = currentColor;

                // // Debug output to console if density is non-zero
                // if (density > 0)
                // {
                //     Debug.Log($"Particle density: {density}, t: {t}, color: {currentColor}");
                // }
            }
        }
    }

    float NormalRandom()
    {
        float u1 = Random.Range(0.0001f, 0.9999f); // Avoid 0 which could cause log(0)
        float u2 = Random.Range(0f, 1f);

        // Box-Muller transform with safety checks
        float result = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);

        // Clamp result to prevent extreme values
        return Mathf.Clamp(result, -3f, 3f);
    }

    // We need to handle when objects get destroyed to prevent trail issues
    void OnDestroy()
    {
        // Clean up all particle trails to prevent memory leaks
        foreach (var particle in particles)
        {
            if (particle.trail != null)
            {
                Destroy(particle.trail);
            }
        }
    }
}