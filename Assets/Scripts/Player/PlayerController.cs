using System.Linq;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float Sensitivity = 1.5f;

    public float WalkingSpeed = 5f;

    public float JumpForce = 1f;

    public float GravityFactor = 3f;

    public float MaxInteractionDistance = 6f;

    public Transform CameraTransform;

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        CameraTransform = GameObject.Find("Main Camera").transform;
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

    private bool PlayerIntersectsVoxel(Vector3Int voxelPos)
    {
        var voxelSurfaceWorldPos = VoxelPosConverter.GetVoxelTopCenterSurfaceWorldPos(voxelPos);

        var hits = Physics.BoxCastAll(
            voxelSurfaceWorldPos, 
            new Vector3(.5f, .5f, 0f),
            Vector3.down,
            Quaternion.identity,
            0.5f
        );
        if(hits.Length > 0)
        {
            if(hits.Any(h => h.transform.gameObject == gameObject))
            {
                return true;
            }
        }
        return false;
    }

    private void HandleWorldInteractions()
    {
        if(Input.GetButtonDown("Fire1"))
        {
            var world = _worldGen.VoxelWorld;
            if(world != null)
            {
                var voxelPos = GetTargetedVoxelPos(true);                
                if(voxelPos.Item1.HasValue)                
                {
                    if(!PlayerIntersectsVoxel(voxelPos.Item1.Value))
                    {
                        // If player places block on the ground, placement direction is the player's look direction
                        var placementDir = voxelPos.Item2.Value;
                        if(placementDir == BlockFace.Bottom)
                        {
                            placementDir = BlockFaceHelper.GetBlockFaceFromVector(GetClosestCardinalLookDirection()).Value;
                        }

                        world.SetVoxelAndRebuild(voxelPos.Item1.Value, 6, placementDir);
                    }                   
                }
            }
        }
        if(Input.GetButtonDown("Fire2"))
        {
            var world = _worldGen.VoxelWorld;
            if(world != null)
            {
                var voxelPos = GetTargetedVoxelPos(false);
                if(voxelPos.Item1.HasValue)
                {
                    world.SetVoxelAndRebuild(voxelPos.Item1.Value, 0);
                }
            }
        }
    }

    private Vector3 GetClosestCardinalLookDirection()
    {
        var cardinalDirections = new Vector3[]
        {
            Vector3.forward,
            Vector3.back,
            Vector3.left,
            Vector3.right
        };

        var closestDir = Vector3.zero;
        var smallestAngle = float.MaxValue;
        foreach(var dir in cardinalDirections)
        {
            var currentAngle = Vector3.Angle(transform.forward, dir);
            if(currentAngle < smallestAngle)
            {
                smallestAngle = currentAngle;
                closestDir = dir;
            }
        }

        return closestDir;
    }

    private void HandleMouseLook()
    {
        var my = Input.GetAxis("Mouse Y") * Sensitivity;
        CameraTransform.Rotate(Vector3.left, my);        

        var mx = Input.GetAxis("Mouse X") * Sensitivity;
        transform.Rotate(Vector3.up, mx);

        // Clamp up/down look to avoid inverting the camera
        if(CameraTransform.up.y < 0)
        {
            CameraTransform.Rotate(Vector3.left, -my);
        }
    }

    private void HandleMovement()
    {
        // Movement
        var forward = Input.GetAxis("Vertical");
        var right = Input.GetAxis("Horizontal");
        var forwardXZ = new Vector3(CameraTransform.forward.x, 0, CameraTransform.forward.z).normalized;
        var rightXZ = new Vector3(CameraTransform.right.x, 0, CameraTransform.right.z).normalized;
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

    private (Vector3Int?, BlockFace?) GetTargetedVoxelPos(bool surfaceVoxel)
    {
        _debugLastRay = new Ray(CameraTransform.position, CameraTransform.forward);
        _debugLastHit = null;
        if(Physics.Raycast(
            CameraTransform.position, 
            CameraTransform.forward, 
            out var hitInfo, 
            LayerMask.GetMask("Voxels")))
        {
            if(hitInfo.distance <= MaxInteractionDistance)
            {
                _debugLastHit = hitInfo.point;
                var normalDirection = surfaceVoxel ? 0.5f : -0.5f;
                var voxelCenterWorldPos = hitInfo.point + hitInfo.normal * normalDirection;
                var voxelPos = VoxelPosConverter.GetVoxelPosFromWorldPos(voxelCenterWorldPos);

                var voxelFace = BlockFaceHelper.GetBlockFaceFromVector(hitInfo.normal.normalized);
                if(voxelFace.HasValue)
                {
                    voxelFace = BlockFaceHelper.GetOppositeFace(voxelFace.Value);
                }
                else
                {
                    voxelFace = BlockFaceHelper.GetBlockFaceFromVector(GetClosestCardinalLookDirection());
                }

                return (voxelPos, voxelFace);
            }
        }
        return (null, null);
    }

    private bool IsGrounded()
    {
        var distanceToPlayerBottom = _controller.bounds.size.y / 2f;
        return Physics.Raycast(
            transform.position, 
            Vector3.down, 
            distanceToPlayerBottom + 0.1f,
            LayerMask.GetMask("Voxels"));
    }

    private CharacterController _controller;

    private Vector3 _velocity;

    private WorldGenerator _worldGen;

    private Ray? _debugLastRay;

    private Vector3? _debugLastHit;

    private Vector3? _lastPlacedVoxel;

    void OnDrawGizmos()
    {
        if(_lastPlacedVoxel != null) Gizmos.DrawCube(_lastPlacedVoxel.Value, Vector3.one);
        //if(_debugLastRay != null) Gizmos.DrawRay(_debugLastRay.Value);
        //if(_debugLastHit != null) Gizmos.DrawSphere(_debugLastHit.Value, .15f);
    }
}
