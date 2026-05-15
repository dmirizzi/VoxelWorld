using UnityEngine;

// Basic fly camera for the WorldGenPrototyping scene.
// Hold right mouse button to look around. WASD/QE to move. Scroll wheel to change speed.
public class FlyCamera : MonoBehaviour
{
    public float MoveSpeed  = 50f;
    public float LookSpeed  = 2f;
    public float SpeedScale = 1.2f;

    private float _yaw;
    private float _pitch;

    void Start()
    {
        var angles = transform.eulerAngles;
        _yaw   = angles.y;
        _pitch = angles.x;
    }

    void Update()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
            MoveSpeed = Mathf.Max(1f, MoveSpeed * Mathf.Pow(SpeedScale, scroll * 10f));

        if (Input.GetMouseButton(1))
        {
            _yaw   += Input.GetAxis("Mouse X") * LookSpeed;
            _pitch -= Input.GetAxis("Mouse Y") * LookSpeed;
            _pitch  = Mathf.Clamp(_pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        float speed = MoveSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftShift)) speed *= 3f;

        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward;
        if (Input.GetKey(KeyCode.D)) move += transform.right;
        if (Input.GetKey(KeyCode.A)) move -= transform.right;
        if (Input.GetKey(KeyCode.E)) move += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

        transform.position += move.normalized * speed;
    }
}
