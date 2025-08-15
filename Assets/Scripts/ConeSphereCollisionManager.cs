using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// Updated Manager script to handle all cone-sphere collisions with temporary color changes
public class ConeSphereCollisionManager : MonoBehaviour
{
    [Header("Object References")]
    [SerializeField] private List<GameObject> cones = new List<GameObject>();
    [SerializeField] private List<GameObject> spheres = new List<GameObject>();

    [Header("Color Settings")]
    [SerializeField] private Color originalSphereColor = Color.white;
    [SerializeField] private Color collisionColor = Color.green;

    [Header("User Study Interface Reference")]
    [SerializeField] private UserStudyInterface userStudyInterface; // Reference to your main script

    // Track active collisions for each sphere (multiple cones might collide with same sphere)
    private Dictionary<GameObject, HashSet<GameObject>> sphereActiveCollisions = new Dictionary<GameObject, HashSet<GameObject>>();
    private Dictionary<GameObject, ConeCollisionHandler> coneHandlers = new Dictionary<GameObject, ConeCollisionHandler>();

    // Track collision events for data recording
    private Dictionary<string, float> collisionStartTimes = new Dictionary<string, float>();
    private int totalCollisionCount = 0;

    void Start()
    {
        InitializeSystem();

        // Try to find UserStudyInterface if not assigned
        if (userStudyInterface == null)
        {
            userStudyInterface = FindObjectOfType<UserStudyInterface>();
            if (userStudyInterface == null)
            {
                Debug.LogWarning("UserStudyInterface not found! CSV recording will be disabled.");
            }
        }
    }

    private void InitializeSystem()
    {
        // Initialize spheres
        foreach (GameObject sphere in spheres)
        {
            if (sphere != null)
            {
                // Set initial color to white
                Renderer renderer = sphere.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = originalSphereColor;
                }

                // Initialize collision tracking
                sphereActiveCollisions[sphere] = new HashSet<GameObject>();

                // Ensure sphere has a collider
                if (sphere.GetComponent<Collider>() == null)
                {
                    sphere.AddComponent<SphereCollider>();
                    Debug.Log($"Added SphereCollider to {sphere.name}");
                }
            }
        }

        // Setup cones with collision handlers
        foreach (GameObject cone in cones)
        {
            if (cone != null)
            {
                // Add collision handler component
                ConeCollisionHandler handler = cone.GetComponent<ConeCollisionHandler>();
                if (handler == null)
                {
                    handler = cone.AddComponent<ConeCollisionHandler>();
                }
                handler.Initialize(this);
                coneHandlers[cone] = handler;

                // Ensure cone has necessary components
                if (cone.GetComponent<Rigidbody>() == null)
                {
                    Rigidbody rb = cone.AddComponent<Rigidbody>();
                    Debug.Log($"Added Rigidbody to {cone.name}");
                }

                if (cone.GetComponent<Collider>() == null)
                {
                    MeshCollider mc = cone.AddComponent<MeshCollider>();
                    mc.convex = true; // Required for Rigidbody
                    Debug.Log($"Added MeshCollider to {cone.name}");
                }
            }
        }

        Debug.Log($"Initialized Cone-Sphere Collision System: {cones.Count} cones, {spheres.Count} spheres");
    }

    public void OnConeCollidedWithSphere(GameObject cone, GameObject sphere)
    {
        // Check if this is one of our tracked spheres
        if (!spheres.Contains(sphere)) return;

        // Add this cone to the active collisions for this sphere
        if (!sphereActiveCollisions[sphere].Contains(cone))
        {
            sphereActiveCollisions[sphere].Add(cone);

            // If this is the first cone colliding with this sphere, change color to green
            if (sphereActiveCollisions[sphere].Count == 1)
            {
                ChangeSphereColor(sphere, collisionColor);
                Debug.Log($"Sphere {sphere.name} turned GREEN (collision started with {cone.name})");
            }

            // Record collision start time using UserStudyInterface's time
            string collisionKey = $"{cone.name}_{sphere.name}";
            collisionStartTimes[collisionKey] = userStudyInterface != null ? userStudyInterface.GetCurrentTime() : Time.time;
            totalCollisionCount++;

            // Record to CSV through UserStudyInterface
            if (userStudyInterface.currentMode == OperationMode.Study)
                RecordCollisionToCSV(cone, sphere, "COLLISION_START");
        }
    }

    public void OnConeStoppedCollidingWithSphere(GameObject cone, GameObject sphere)
    {
        // Check if this is one of our tracked spheres
        if (!spheres.Contains(sphere)) return;

        // Remove this cone from the active collisions for this sphere
        if (sphereActiveCollisions[sphere].Contains(cone))
        {
            sphereActiveCollisions[sphere].Remove(cone);

            // If no more cones are colliding with this sphere, change color back to white
            if (sphereActiveCollisions[sphere].Count == 0)
            {
                ChangeSphereColor(sphere, originalSphereColor);
                Debug.Log($"Sphere {sphere.name} turned WHITE (no more collisions)");
            }
            else
            {
                Debug.Log($"Sphere {sphere.name} stays GREEN ({sphereActiveCollisions[sphere].Count} cones still colliding)");
            }

            // Calculate collision duration using UserStudyInterface's time
            string collisionKey = $"{cone.name}_{sphere.name}";
            float collisionDuration = 0f;
            if (collisionStartTimes.ContainsKey(collisionKey))
            {
                float currentTime = userStudyInterface != null ? userStudyInterface.GetCurrentTime() : Time.time;
                collisionDuration = currentTime - collisionStartTimes[collisionKey];
                collisionStartTimes.Remove(collisionKey);
            }

            // Record to CSV through UserStudyInterface
            if (userStudyInterface.currentMode == OperationMode.Study)
                RecordCollisionToCSV(cone, sphere, "COLLISION_END", collisionDuration);
        }
    }

    private void RecordCollisionToCSV(GameObject cone, GameObject sphere, string eventType, float duration = 0f)
    {
        if (userStudyInterface != null)
        {
            // Get the actual time value from UserStudyInterface (not Unity's Time.time)
            float currentTime = userStudyInterface.GetCurrentTime();
            float currentPM25 = userStudyInterface.GetCurrentBurstParameter();

            // Create descriptive string for the drilling point parameter
            string drillingPointInfo = $"{eventType}_{cone.name}_{sphere.name}";
            if (duration > 0)
            {
                drillingPointInfo += $"_Duration:{duration:F2}s";
            }

            // Call SaveToCSV with appropriate parameters
            // Using UserStudyInterface's time as timeToResponse, collision count as responseTimes
            userStudyInterface.SaveToCSV(
                // timeToResponse: -1,
                // responseTimes: totalCollisionCount,
                currentBurstParameter: currentPM25,
                // timeToPM25: -1,
                timeToPoint: currentTime,
                TaskPoint: drillingPointInfo
            );

            Debug.Log($"Recorded to CSV: {drillingPointInfo} at UserStudy time {currentTime:F2}");
        }
    }

    private float GetCurrentPM25FromUserStudy()
    {
        // Simply call the public getter method from UserStudyInterface
        if (userStudyInterface != null)
        {
            return userStudyInterface.GetCurrentBurstParameter();
        }
        return -1f;
    }

    private void ChangeSphereColor(GameObject sphere, Color color)
    {
        Renderer renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }

    // Get current collision status
    public bool IsSphereBeingCollided(GameObject sphere)
    {
        return sphereActiveCollisions.ContainsKey(sphere) && sphereActiveCollisions[sphere].Count > 0;
    }

    // Get number of cones currently colliding with a sphere
    public int GetCollisionCount(GameObject sphere)
    {
        if (sphereActiveCollisions.ContainsKey(sphere))
        {
            return sphereActiveCollisions[sphere].Count;
        }
        return 0;
    }

    // Get total number of spheres currently being collided with
    public int GetActiveCollisionSphereCount()
    {
        int count = 0;
        foreach (var sphere in sphereActiveCollisions.Keys)
        {
            if (sphereActiveCollisions[sphere].Count > 0)
            {
                count++;
            }
        }
        return count;
    }

    // Reset all spheres to white (useful for resetting the scene)
    public void ResetAllSpheres()
    {
        foreach (GameObject sphere in spheres)
        {
            if (sphere != null)
            {
                sphereActiveCollisions[sphere].Clear();
                ChangeSphereColor(sphere, originalSphereColor);
            }
        }
        collisionStartTimes.Clear();
        totalCollisionCount = 0;
        Debug.Log("All spheres reset to white");
    }

    // Inner class for handling collisions on each cone
    public class ConeCollisionHandler : MonoBehaviour
    {
        private ConeSphereCollisionManager manager;

        public void Initialize(ConeSphereCollisionManager manager)
        {
            this.manager = manager;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (manager != null)
            {
                manager.OnConeCollidedWithSphere(gameObject, collision.gameObject);
            }
        }

        void OnCollisionExit(Collision collision)
        {
            if (manager != null)
            {
                manager.OnConeStoppedCollidingWithSphere(gameObject, collision.gameObject);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (manager != null)
            {
                manager.OnConeCollidedWithSphere(gameObject, other.gameObject);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (manager != null)
            {
                manager.OnConeStoppedCollidingWithSphere(gameObject, other.gameObject);
            }
        }
    }
}
