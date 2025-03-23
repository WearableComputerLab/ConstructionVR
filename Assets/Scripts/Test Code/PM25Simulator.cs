using UnityEngine;
using System.Collections.Generic;

public class PM25Simulator : MonoBehaviour
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

    [Header("Physics & Movement Settings")]
    [Tooltip("Strength of the random diffusive motion.")]
    public float diffusionStrength = 0.5f;
    
    [Tooltip("Wind direction affecting particle movement.")]
    public Vector3 windDirection = new Vector3(1, 0, 0);
    
    [Tooltip("Wind speed affecting particle movement.")]
    public float windSpeed = 0.5f;
    
    [Tooltip("Gravitational settling speed (PM2.5 particles settle very slowly).")]
    public float gravityStrength = 0.1f;

    // Internal list to keep track of spawned particles.
    private List<GameObject> particles;

    void Start()
    {
        particles = new List<GameObject>();

        // Spawn particles within the defined area.
        for (int i = 0; i < particleCount; i++)
        {
            Vector3 spawnPos = spawnCenter + new Vector3(
                Random.Range(-spawnRange.x * 0.5f, spawnRange.x * 0.5f),
                Random.Range(-spawnRange.y * 0.5f, spawnRange.y * 0.5f),
                Random.Range(-spawnRange.z * 0.5f, spawnRange.z * 0.5f)
            );

            // Instantiate the particle and set its scale.
            GameObject particle = Instantiate(particlePrefab, spawnPos, Quaternion.identity);
            particle.transform.localScale = Vector3.one * particleSize;

            // Parent particles under this simulator for organization.
            particle.transform.parent = transform;

            particles.Add(particle);
        }
    }

    void Update()
    {
        // Update each particle's position every frame.
        foreach (GameObject particle in particles)
        {
            if (particle == null) continue;

            // Calculate a random diffusion vector (simulating Brownian motion).
            Vector3 randomDiffusion = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ).normalized * diffusionStrength * Time.deltaTime;

            // Calculate wind-induced movement.
            Vector3 windMovement = windDirection.normalized * windSpeed * Time.deltaTime;

            // Calculate gravitational settling (downward movement).
            Vector3 gravityMovement = Vector3.down * gravityStrength * Time.deltaTime;

            // Combine all movement components.
            Vector3 movement = randomDiffusion + windMovement + gravityMovement;
            particle.transform.position += movement;
        }
    }
}
