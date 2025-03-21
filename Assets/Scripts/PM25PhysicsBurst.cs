using UnityEngine;
using System.Collections.Generic;

public class PM25PhysicsBurst : MonoBehaviour
{
    [Header("Particle Settings")]
    public GameObject particlePrefab;
    public int particleCount = 500;
    public float particleSize = 0.05f;

    [Header("Emission Settings")]
    public Transform drillingPoint;
    public float emissionDuration = 2f;
    private float emissionTimer;

    [Header("Convection-Diffusion Settings")]
    public Vector3 initialUpwardVelocity = new Vector3(0, 1.5f, 0);
    public Vector3 windVelocity = new Vector3(0.2f, 0, 0);
    public float diffusionCoefficient = 0.1f;
    public float gravityStrength = 0.2f;
    public float movementSpeedMultiplier = 1f;

    [Header("Density-Based Color Settings")]
    public Color lowDensityColor = new Color(1,1,1,0.2f);
    public Color highDensityColor = new Color(0,0,0,0.9f);
    public float densityCheckRadius = 0.2f;
    public int maxDensity = 15;

    private List<ParticleData> particles = new List<ParticleData>();

    class ParticleData
    {
        public GameObject obj;
        public Vector3 velocity;
    }

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

        foreach (ParticleData particle in particles)
        {
            if (particle.obj == null) continue;

            // Physics-driven convection-diffusion equation
            Vector3 convectionVelocity = particle.velocity + windVelocity + Vector3.down * gravityStrength;
            Vector3 convectionStep = convectionVelocity * dt;

            float diffusionScale = Mathf.Sqrt(2 * diffusionCoefficient * dt);
            Vector3 diffusionStep = new Vector3(
                NormalRandom() * diffusionScale,
                NormalRandom() * diffusionScale,
                NormalRandom() * diffusionScale
            );

            Vector3 gravityStep = Vector3.down * gravityStrength * dt;

            particle.obj.transform.position += (convectionStep + diffusionStep + gravityStep) * movementSpeedMultiplier;

            // Gradually reduce particle upward velocity (simulate burst slowing)
            particle.velocity = Vector3.Lerp(particle.velocity, Vector3.zero, 0.5f * dt);
        }
    }

    void EmitParticles()
    {
        int particlesToEmit = Mathf.CeilToInt(particleCount / emissionDuration * Time.deltaTime);
        for (int i = 0; i < particlesToEmit; i++)
        {
            GameObject particleObj = Instantiate(particlePrefab, drillingPoint.position, Quaternion.identity, transform);
            particleObj.transform.localScale = Vector3.one * particleSize;

            // Initial velocity: upward + random radial burst
            Vector3 burstDir = (Vector3.up + Random.insideUnitSphere * 0.4f).normalized;
            Vector3 initialVelocity = Vector3.Scale(initialUpwardVelocity, burstDir);

            ParticleData pd = new ParticleData
            {
                obj = particleObj,
                velocity = initialVelocity
            };
            particles.Add(pd);
        }
    }

    void UpdateParticleColors()
    {
        foreach (ParticleData particle in particles)
        {
            if (particle.obj == null) continue;

            Collider[] neighbors = Physics.OverlapSphere(particle.obj.transform.position, densityCheckRadius);
            int density = neighbors.Length;

            float t = Mathf.Clamp01((float)density / maxDensity);
            Color currentColor = Color.Lerp(lowDensityColor, highDensityColor, t);

            Renderer rend = particle.obj.GetComponent<Renderer>();
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
