using UnityEngine;

// Attach to the Main Camera in the prototyping scene.
// Left-drag  → orbit    Right-drag → pan    Scroll → zoom
public class OrbitCamera : MonoBehaviour
{
    [Header("Sensitivity")]
    public float OrbitSensitivity = 3f;    // degrees per mouse-axis unit
    public float PanSensitivity   = 0.02f; // fraction of distance per mouse-axis unit
    public float ZoomSensitivity  = 0.75f;  // fractional zoom per scroll unit (~0.1 per notch)

    [Header("Limits")]
    public float MinDistance =    1f;
    public float MaxDistance = 5000f;
    public float MinPitch    =  -80f;
    public float MaxPitch    =   80f;

    private Vector3 _pivot;
    private float   _distance = 50f;
    private float   _yaw      = -25f;
    private float   _pitch    =  20f;

    void Start() => ApplyTransform();

    // Called by CaveGenPrototypingController after each regeneration.
    public void Frame(Vector3 pivot, float distance)
    {
        _pivot    = pivot;
        _distance = Mathf.Clamp(distance, MinDistance, MaxDistance);
        _yaw      = -25f;
        _pitch     =  20f;
        ApplyTransform();
    }

    void Update()
    {
        bool dirty = false;

        if (Input.GetMouseButton(0))
        {
            _yaw   += Input.GetAxis("Mouse X") * OrbitSensitivity;
            _pitch -= Input.GetAxis("Mouse Y") * OrbitSensitivity;
            _pitch  = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
            dirty   = true;
        }

        if (Input.GetMouseButton(1))
        {
            float scale = _distance * PanSensitivity;
            _pivot -= transform.right * (Input.GetAxis("Mouse X") * scale);
            _pivot -= transform.up    * (Input.GetAxis("Mouse Y") * scale);
            dirty   = true;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            _distance = Mathf.Clamp(_distance * (1f - scroll * ZoomSensitivity), MinDistance, MaxDistance);
            dirty     = true;
        }

        if (dirty) ApplyTransform();
    }

    private void ApplyTransform()
    {
        var rot = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.SetPositionAndRotation(_pivot - rot * Vector3.forward * _distance, rot);
    }
}
