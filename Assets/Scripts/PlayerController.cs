using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public bool Grounded;

    public float Sensitivity = 1.5f;

    public float WalkingSpeed = 5f;

    public float JumpForce = 5f;

    private Rigidbody _rigidBody;

    private CapsuleCollider _collider;

    private Transform _cameraTransform;

    void Start()
    {
        _rigidBody = GetComponent<Rigidbody>();
        _collider = GetComponent<CapsuleCollider>();
        _cameraTransform = GameObject.Find("Main Camera").transform;
    }

    // Update is called once per frame
    void Update()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Mouse Look
        var mx = Input.GetAxis("Mouse X") * Sensitivity;
        var my = Input.GetAxis("Mouse Y") * Sensitivity;
        _cameraTransform.rotation *= Quaternion.Euler(-my, mx, 0f);
        var angles = _cameraTransform.rotation.eulerAngles;
        _cameraTransform.rotation = Quaternion.Euler(angles.x, angles.y, 0);

        // Movement
        var forward = Input.GetAxis("Vertical");
        var right = Input.GetAxis("Horizontal");
        var forwardXZ = new Vector3(_cameraTransform.forward.x, 0, _cameraTransform.forward.z);
        var rightXZ = new Vector3(_cameraTransform.right.x, 0, _cameraTransform.right.z);
        var oldVelocityY = _rigidBody.velocity.y;

        _rigidBody.velocity = (forwardXZ * forward + rightXZ * right) * WalkingSpeed + Vector3.up * oldVelocityY;

        // Jumping
        Grounded = IsGrounded();
        if(Input.GetButtonDown("Jump") && IsGrounded())
        {
            _rigidBody.AddForce(Vector3.up * JumpForce, ForceMode.Impulse);
        }
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, _collider.bounds.size.y / 2 + 0.1f);
    }
}
