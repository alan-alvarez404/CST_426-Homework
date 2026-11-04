using UnityEngine;

public class Vector4Example : MonoBehaviour
{
    void Start()
    {
        // Create a Vector4 with explicit components
        Vector4 myVector = new Vector4(1.0f, 2.0f, 3.0f, 4.0f);

        // Access components via fields
        Debug.Log("x = " + myVector.x); // 1.0
        Debug.Log("w = " + myVector.w); // 4.0

        // Use built‑in methods
        Vector4 normalized = myVector.normalized; // unit length
        Debug.Log("Normalized magnitude: " + normalized.magnitude);

        // Linear interpolation between two Vector4s
        Vector4 start = new Vector4(0, 0, 0, 0);
        Vector4 end   = new Vector4(1, 1, 1, 1);
        Vector4 lerpVal = Vector4.Lerp(start, end, 0.5f);
        Debug.Log("Lerp result: " + lerpVal);

        // Example: using Vector4 for color (implicit conversion)
        Color color = new Vector4(0.3f, 0.4f, 0.6f, 1.0f);
        Debug.Log("Color from Vector4: " + color);
    }
}