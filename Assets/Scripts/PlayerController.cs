using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float Sensitivity = 1.5f;

    public float WalkingSpeed = 5f;

    public float JumpForce = 1f;

    public float GravityFactor = 3f;

    private Transform _cameraTransform;

    private CharacterController _controller;

    private Vector3 _velocity;

    void Start()
    {
        _controller = GetComponent<CharacterController>();
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
        _controller.Move((forwardXZ * forward + rightXZ * right) * WalkingSpeed * Time.deltaTime);

        // Jumping / Falling
        if(IsGrounded())
        {
            _velocity = Vector3.zero;
            if(Input.GetButtonDown("Jump"))
            {
                _velocity = Vector3.up * JumpForce;
            }
        }
        else
        {
            _velocity += Physics.gravity * Time.deltaTime * GravityFactor;
        }
        _controller.Move(_velocity * Time.deltaTime);
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, _controller.bounds.size.y / 2 + 0.1f);
    }
}
