using UnityEngine;

public class FollowCameraHeight : MonoBehaviour
{
    [SerializeField] Transform flythroughCamera;
    [SerializeField] float maxWorldY = 120f;

    // Simple script so that the Gaze Target goes up so that the camera sees the entire rocket
    void LateUpdate()
    {
        if (flythroughCamera == null)
            return;

        float desiredWorldY = Mathf.Min(flythroughCamera.position.y, maxWorldY);

        Vector3 worldPosition = transform.position;
        worldPosition.y = desiredWorldY;
        transform.position = worldPosition;
    }
    
}