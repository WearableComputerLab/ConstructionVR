using UnityEngine;
using System.Collections.Generic;

public class PM25BurstSpray : MonoBehaviour
{
    [Header("PM2.5 Particle Settings")]
    public GameObject particlePrefab;
    public int particleCount = 500;
    public float particleSize = 0.05f;

    [Header("Drilling Spray Settings")]
    public Transform drillingPoint;
    public float sprayUpwardForce = 1.5f; // initial upward speed
    public float burstForce = 0.5f; // radial burst force
    public float emissionDuration = 3f;
    private float emissionTimer;

    [Header("Movement Settings")]
    public Vector3 windVelocity = new Vector3(0.1f, 0, 0);
    public float diffusionCoefficient = 0.05f;
    public float gravityStrength = 0.05f;
    public float movementSpeedMultiplier = 1f;

    [Header("Density-based Color Settings")]
    public Color lowDensityColor = new Color(1,1,1,0.2f);
    public Color highDensityColor = new Color(0,0,0,0.9f);
    public float densityCheckRadius = 0.2f;
    public int maxDensity = 20;

    private List<GameObject> particles = new List<GameObject>();

    void Start()
    {
        emissionTimer = emissionDuration;
        InvokeRepeating("UpdateParticleColors", 0.5f, 0.5f);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (emissionTimer > 0)
        {
            EmitParticles();
            emissionTimer -= dt;
        }

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
            Vector3 spawnPos = drillingPoint.position;
            GameObject particle = Instantiate(particlePrefab, spawnPos, Quaternion.identity, transform);
            particle.transform.localScale = Vector3.one * particleSize;

            Rigidbody rb = particle.GetComponent<Rigidbody>();
            if (rb == null) rb = particle.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.drag = 1f;

            // Shoot upward, then burst outward
            Vector3 burstDirection = (Vector3.up + Random.insideUnitSphere * 0.5f).normalized;
            Vector3 initialForce = Vector3.up * sprayUpwardForce + burstDirection * burstForce;
            rb.AddForce(initialForce, ForceMode.Impulse);

            particles.Add(particle);
        }
    }

    void UpdateParticleColors()
    {
        foreach (GameObject particle in particles)
        {
            if (particle == null) continue;

            Collider[] neighbors = Physics.OverlapSphere(particle.transform.position, densityCheckRadius);
            int density = neighbors.Length;

            float t = Mathf.Clamp01((float)density / maxDensity);
            Color currentColor = Color.Lerp(lowDensityColor, highDensityColor, t);

            Renderer rend = particle.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = currentColor;
            }
        }
    }

    float NormalRandom()
    {
        float u1 = Random.Range(0f, 1f);
        float u2 = Random.Range(0f, 1f);
        return Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
    }
}
