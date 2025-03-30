using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Drilling : MonoBehaviour
{
    // Rotation speed in degrees per second
    public Vector3 rotationSpeed = new Vector3(0, 30, 0);

    // Update is called once per frame
    void Update()
    {
        // Rotate the object around its local axes based on rotationSpeed
        transform.Rotate(rotationSpeed * Time.deltaTime);
    }
}
