using UnityEngine;

public class MobileCameraZoom : MonoBehaviour
{
    public Camera cam;

    public float zoomSpeed = 0.1f;
    public float minZoom = 3f;
    public float maxZoom = 2000f;

    public float dragSpeed = 1f;

    void Update()
    {
        // PAN (1 ngón tay)
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Moved)
            {
                PanCamera(touch);
            }
        }

        // ZOOM (2 ngón tay)
        if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            Vector2 touch0Prev = touch0.position - touch0.deltaPosition;
            Vector2 touch1Prev = touch1.position - touch1.deltaPosition;

            float prevDistance = (touch0Prev - touch1Prev).magnitude;
            float currentDistance = (touch0.position - touch1.position).magnitude;

            float difference = currentDistance - prevDistance;

            Zoom(difference * zoomSpeed);
        }
    }

    void PanCamera(Touch touch)
    {
        Vector3 prevPos = cam.ScreenToWorldPoint(
            touch.position - touch.deltaPosition
        );

        Vector3 currentPos = cam.ScreenToWorldPoint(
            touch.position
        );

        Vector3 direction = prevPos - currentPos;

        cam.transform.position += direction * dragSpeed;
    }

    void Zoom(float increment)
    {
        cam.orthographicSize -= increment;

        cam.orthographicSize = Mathf.Clamp(
            cam.orthographicSize,
            minZoom,
            maxZoom
        );
    }
}