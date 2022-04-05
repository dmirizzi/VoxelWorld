using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float Sensitivity = 1.5f;

    public float WalkingSpeed = 5f;

    public float JumpForce = 1f;

    public float GravityFactor = 3f;

    public float MaxInteractionDistance = 6f;

    private Transform _cameraTransform;

    private CharacterController _controller;

    private Vector3 _velocity;

    private WorldGenerator _worldGen;

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _cameraTransform = GameObject.Find("Main Camera").transform;
        _worldGen = GameObject.FindObjectsOfType<WorldGenerator>()[0];
    }

    // Update is called once per frame
    void Update()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        HandleMouseLook();
        HandleMovement();
        HandleJumping();
        HandleWorldInteractions();

        _controller.Move(_velocity * Time.deltaTime);
    }

    private void HandleWorldInteractions()
    {
        if(Input.GetButtonDown("Fire1"))
        {
            var world = _worldGen.VoxelWorld;
            if(world != null)
            {
                var voxelPos = GetTargetedVoxelPos(true);
                if(voxelPos != null)
                {
                    world.SetVoxelAndRebuild(voxelPos.Value, VoxelType.Cobblestone);
                }
            }
        }
        if(Input.GetButtonDown("Fire2"))
        {
            var world = _worldGen.VoxelWorld;
            if(world != null)
            {
                var voxelPos = GetTargetedVoxelPos(false);
                if(voxelPos != null)
                {
                    world.SetVoxelAndRebuild(voxelPos.Value, VoxelType.Empty);
                }
            }
        }
    }

    private void HandleMouseLook()
    {
        // Mouse Look
        var mx = Input.GetAxis("Mouse X") * Sensitivity;
        var my = Input.GetAxis("Mouse Y") * Sensitivity;
        _cameraTransform.rotation *= Quaternion.Euler(-my, 0f, 0f);
        transform.rotation *= Quaternion.Euler(0f, mx, 0f);

        // Avoid rotation on z-axis
        var angles = _cameraTransform.rotation.eulerAngles;
        _cameraTransform.rotation = Quaternion.Euler(angles.x, angles.y, 0);
        angles = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0, angles.y, 0);
    }

    private void HandleMovement()
    {
        // Movement
        var forward = Input.GetAxis("Vertical");
        var right = Input.GetAxis("Horizontal");
        var forwardXZ = new Vector3(_cameraTransform.forward.x, 0, _cameraTransform.forward.z);
        var rightXZ = new Vector3(_cameraTransform.right.x, 0, _cameraTransform.right.z);
        _controller.Move((forwardXZ * forward + rightXZ * right) * WalkingSpeed * Time.deltaTime);
    }

    private void HandleJumping()
    {
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
    }

    private Vector3Int? GetTargetedVoxelPos(bool surfaceVoxel)
    {
        _debugLastRay = new Ray(_cameraTransform.position, _cameraTransform.forward);
        _debugLastHit = null;
        if(Physics.Raycast(_cameraTransform.position, _cameraTransform.forward, out var hitInfo))
        {
            if(hitInfo.distance <= MaxInteractionDistance)
            {
                _debugLastHit = hitInfo.point;
                var normalDirection = surfaceVoxel ? 0.5f : -0.5f;
                var voxelCenterWorldPos = hitInfo.point + hitInfo.normal * normalDirection;
                var voxelPos = VoxelPosConverter.GetVoxelPosFromWorldPos(voxelCenterWorldPos);
                return voxelPos;
            }
        }
        return null;        
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, _controller.bounds.size.y / 2 + 0.1f);
    }

    private Ray? _debugLastRay;
    private Vector3? _debugLastHit;

    void OnDrawGizmos()
    {
        //if(_debugLastRay != null) Gizmos.DrawRay(_debugLastRay.Value);
        //if(_debugLastHit != null) Gizmos.DrawSphere(_debugLastHit.Value, .15f);
    }
}
