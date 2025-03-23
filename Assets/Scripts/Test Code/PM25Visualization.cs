using UnityEngine;
using System.Collections.Generic;

public class PM25Visualization : MonoBehaviour
{
    [Header("Particle Settings")]
    public GameObject particlePrefab;
    public int particleCount = 300;
    public float particleSize = 0.05f;
    public Vector3 spawnCenter = Vector3.zero;
    public Vector3 spawnRange = new Vector3(1, 1, 1); // Restricted to 1m³ volume

    [Header("Physics Settings")]
    public Vector3 windVelocity = new Vector3(0.2f, 0, 0);
    public float diffusionCoefficient = 0.1f;
    public float gravityStrength = 0.02f;
    public float movementSpeed = 0.5f;

    [Header("Visualization Settings")]
    public Gradient heightColorGradient;
    public float densityRadius = 0.2f;
    public float trailLength = 1.0f;
    public float trailThickness = 0.01f;

    List<GameObject> particles;

    void Start()
    {
        particles = new List<GameObject>();

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

            // Add Trail Renderer for trajectories
            TrailRenderer trail = particle.AddComponent<TrailRenderer>();
            trail.time = trailLength;
            trail.startWidth = trailThickness;
            trail.endWidth = 0;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.material.color = Color.white;

            particles.Add(particle);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime * movementSpeed;

        foreach (GameObject particle in particles)
        {
            if (particle == null) continue;

            Vector3 convection = windVelocity * dt;
            Vector3 diffusion = new Vector3(NormalRandom(), NormalRandom(), NormalRandom()) * Mathf.Sqrt(2 * diffusionCoefficient * dt);
            Vector3 gravity = Vector3.down * gravityStrength * dt;

            Vector3 newPosition = particle.transform.position + convection + diffusion + gravity;
            newPosition = ConstrainToVolume(newPosition);
            particle.transform.position = newPosition;

            // Color particles based on height
            float heightNormalized = Mathf.InverseLerp(spawnCenter.y - spawnRange.y / 2, spawnCenter.y + spawnRange.y / 2, particle.transform.position.y);
            Color heightColor = heightColorGradient.Evaluate(heightNormalized);

            // Density-based transparency
            float density = CalculateDensity(particle.transform.position);
            float alpha = Mathf.Clamp01(density / 10f);

            // Apply visualization
            Renderer rend = particle.GetComponent<Renderer>();
            rend.material.color = new Color(heightColor.r, heightColor.g, heightColor.b, alpha);

            // Match trail color to particle color
            TrailRenderer trail = particle.GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.material.color = new Color(heightColor.r, heightColor.g, heightColor.b, alpha);
            }
        }
    }

    float NormalRandom()
    {
        float u1 = Random.Range(0f, 1f);
        float u2 = Random.Range(0f, 1f);
        return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
    }

    float CalculateDensity(Vector3 pos)
    {
        int count = 0;
        foreach (GameObject p in particles)
        {
            if (Vector3.Distance(p.transform.position, pos) <= densityRadius)
                count++;
        }
        return count;
    }

    Vector3 ConstrainToVolume(Vector3 position)
    {
        return new Vector3(
            Mathf.Clamp(position.x, spawnCenter.x - spawnRange.x / 2, spawnCenter.x + spawnRange.x / 2),
            Mathf.Clamp(position.y, spawnCenter.y - spawnRange.y / 2, spawnCenter.y + spawnRange.y / 2),
            Mathf.Clamp(position.z, spawnCenter.z - spawnRange.z / 2, spawnCenter.z + spawnRange.z / 2)
        );
    }
}