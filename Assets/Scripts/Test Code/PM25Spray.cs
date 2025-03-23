using UnityEngine;
using System.Collections.Generic;

public class PM25Spray : MonoBehaviour
{
    [Header("PM2.5 Particle Settings")]
    public GameObject particlePrefab;
    public int particleCount = 500;
    public float particleSize = 0.05f;

    [Header("Drilling Spray Settings")]
    public Transform drillingPoint; // The point from which particles spray
    public float sprayForce = 0.5f; // initial outward speed from the drill point
    public float emissionDuration = 5f; // how long particles emit from drill
    private float emissionTimer;

    [Header("Movement Settings")]
    public Vector3 windVelocity = new Vector3(0.1f, 0, 0);
    public float diffusionCoefficient = 0.05f;
    public float gravityStrength = 0.01f;
    public float movementSpeedMultiplier = 1f;

    [Header("Color Density Visualization")]
    public Color lowDensityColor = new Color(1,1,1,0.2f); // lighter color
    public Color highDensityColor = new Color(0,0,0,0.9f); // darker color
    public float densityCheckRadius = 0.2f; // radius to check particle density
    public int maxDensity = 20; // maximum particles for darkest color

    private List<GameObject> particles = new List<GameObject>();

    void Start()
    {
        emissionTimer = emissionDuration;
        InvokeRepeating("UpdateParticleColors", 0.5f, 0.5f);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // Emit particles while drilling is active
        if (emissionTimer > 0)
        {
            EmitParticles();
            emissionTimer -= dt;
        }

        // Update particle positions
        foreach (GameObject particle in particles)
        {
            if (particle == null) continue;

            Vector3 convectionStep = windVelocity * dt;
            float diffusionScale = Mathf.Sqrt(2 * diffusionCoefficient * dt);
            Vector3 diffusionStep = new Vector3(
                NormalRandom() * diffusionScale,
                NormalRandom() * diffusionScale,
                NormalRandom() * diffusionScale
            );
            Vector3 gravityStep = Vector3.down * gravityStrength * dt;

            particle.transform.position += (convectionStep + diffusionStep + gravityStep) * movementSpeedMultiplier;
        }
    }

    void EmitParticles()
    {
        int particlesToEmit = Mathf.CeilToInt(particleCount / emissionDuration * Time.deltaTime);
        for (int i = 0; i < particlesToEmit; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere.normalized;
            Vector3 spawnPos = drillingPoint.position + randomDir * 0.02f; // slightly offset from drill point
            GameObject particle = Instantiate(particlePrefab, spawnPos, Quaternion.identity, transform);
            particle.transform.localScale = Vector3.one * particleSize;

            // Give initial spray force
            Rigidbody rb = particle.GetComponent<Rigidbody>();
            if (rb == null) rb = particle.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.drag = 1f;
            rb.AddForce(randomDir * sprayForce, ForceMode.Impulse);

            particles.Add(particle);
        }
    }

    void UpdateParticleColors()
    {
        foreach (GameObject particle in particles)
        {
            if (particle == null) continue;

            // Count nearby particles to estimate local density
            Collider[] neighbors = Physics.OverlapSphere(particle.transform.position, densityCheckRadius);
            int density = neighbors.Length;

            // Map density to color
            float t = Mathf.Clamp01((float)density / maxDensity);
            Color currentColor = Color.Lerp(lowDensityColor, highDensityColor, t);

            // Apply the color
            Renderer rend = particle.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = currentColor;
            }
        }
    }

    // Box-Muller Transform for Gaussian randomness
    float NormalRandom()
    {
        float u1 = Random.Range(0f, 1f);
        float u2 = Random.Range(0f, 1f);
        return Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
    }
}