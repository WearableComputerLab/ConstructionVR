using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PM25ConvectionDiffusion : MonoBehaviour
{
    [Header("Particle Settings")]
    [Tooltip("Prefab for the PM2.5 particle (e.g., a small sphere).")]
    public GameObject particlePrefab;

    [Tooltip("Total number of PM2.5 particles to simulate.")]
    public int particleCount = 500;

    [Tooltip("Size (scale) of each particle.")]
    public float particleSize = 0.1f;

    [Tooltip("Center position for spawning particles.")]
    public Vector3 spawnCenter = Vector3.zero;

    [Tooltip("Extent (width, height, depth) around the center where particles will be spawned.")]
    public Vector3 spawnRange = new Vector3(10, 10, 10);

    [Header("Convection-Diffusion Parameters")]
    [Tooltip("Wind (convection) velocity in m/s.")]
    public Vector3 windVelocity = new Vector3(1, 0, 0);

    [Tooltip("Diffusion coefficient (D) in m²/s.")]
    public float diffusionCoefficient = 0.5f;

    [Tooltip("Gravitational settling speed in m/s (downward).")]
    public float gravityStrength = 0.1f;

    // Internal list to track particles.
    private List<GameObject> particles;

    void Start()
    {
        particles = new List<GameObject>();

        // Spawn particles within a defined box around spawnCenter.
        for (int i = 0; i < particleCount; i++)
        {
            Vector3 spawnPos = spawnCenter + new Vector3(
                Random.Range(-spawnRange.x * 0.5f, spawnRange.x * 0.5f),
                Random.Range(-spawnRange.y * 0.5f, spawnRange.y * 0.5f),
                Random.Range(-spawnRange.z * 0.5f, spawnRange.z * 0.5f)
            );

            GameObject particle = Instantiate(particlePrefab, spawnPos, Quaternion.identity);
            particle.transform.localScale = Vector3.one * particleSize;
            particle.transform.parent = transform;
            particles.Add(particle);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        foreach (GameObject particle in particles)
        {
            if (particle == null) continue;

            // Convection (wind) movement:
            Vector3 convectionStep = windVelocity * dt;

            // Diffusion: Gaussian random step with magnitude sqrt(2 * D * dt)
            float diffusionScale = Mathf.Sqrt(2 * diffusionCoefficient * dt);
            Vector3 diffusionStep = new Vector3(
                NormalRandom() * diffusionScale,
                NormalRandom() * diffusionScale,
                NormalRandom() * diffusionScale
            );

            // Gravitational settling (downward drift):
            Vector3 gravityStep = Vector3.down * gravityStrength * dt;

            // Total displacement is the sum of convection, diffusion, and gravity.
            Vector3 totalStep = convectionStep + diffusionStep + gravityStep;
            particle.transform.position += totalStep;
        }
    }

    // Generate a normally distributed random number using the Box-Muller transform.
    float NormalRandom()
    {
        float u1 = Random.Range(0f, 1f);
        float u2 = Random.Range(0f, 1f);
        return Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
    }
}