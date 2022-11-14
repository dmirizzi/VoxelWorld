using System;
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
        _actionBar = GetComponent<PlayerActionBarController>();
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

    void OnGUI()
    {
        var targetedVoxel = GetTargetedVoxelPos(false);
        var targetedVoxelType = targetedVoxel.Item1.HasValue ? _worldGen?.VoxelWorld.GetVoxel(targetedVoxel.Item1.Value) : null;

        GUI.Label(new Rect(10, 10, 1500, 18), $"LookDir={GetLookDir()} | TargetedVoxelType: {targetedVoxelType}");
    }

    private bool PlayerIntersectsVoxel(Vector3Int voxelPos)
    {
        var voxelSurfaceWorldPos = VoxelPosHelper.GetVoxelTopCenterSurfaceWorldPos(voxelPos);

        var hits = Physics.BoxCastAll(
            voxelSurfaceWorldPos, 
            new Vector3(.5f, .5f, .5f),
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
            if(_actionBar.CurrentlySelectedItem != null )
            {
                var blockType = _actionBar.CurrentlySelectedItem.BlockType;
                if(!string.IsNullOrEmpty(blockType))
                {
                    PlaceBlock(blockType);
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
        if(Input.GetButtonDown("Use"))
        {
            var world = _worldGen.VoxelWorld;
            if(world != null)
            {
                var voxelPos = GetTargetedVoxelPos(false);
                if(voxelPos.Item1.HasValue)
                {
                    var voxelType = world.GetVoxel(voxelPos.Item1.Value);
                    var blockType = BlockTypeRegistry.GetBlockType(voxelType);
                    if(blockType != null)
                    {
                        blockType.OnUse(_worldGen.VoxelWorld, voxelPos.Item1.Value, GetLookDir());
                    }                    
                }
            }
        }        
    }

    private void PlaceBlock(string blockType)
    {
        var world = _worldGen.VoxelWorld;
        if(world != null)
        {
            var voxelPos = GetTargetedVoxelPos(true);                
            if(voxelPos.Item1.HasValue)                
            {
                var targetVoxelPos = voxelPos.Item1.Value;
                if(!PlayerIntersectsVoxel(voxelPos.Item1.Value))
                {
                    var placementDir = voxelPos.Item2.Value;

                    if(world.GetVoxel(targetVoxelPos) != 0)
                    {
                        return;
                    }

                    world.SetVoxelAndRebuild(
                        targetVoxelPos, 
                        BlockDataRepository.GetBlockTypeId(blockType), 
                        placementDir,
                        GetLookDir());
                }                   
            }
        }
    }

    private BlockFace GetLookDir() => BlockFaceHelper.GetBlockFaceFromVector(GetClosestCardinalLookDirection()).Value;

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
                _debugLastHitNormal = hitInfo.normal;

                Vector3Int voxelPos;
                _debugTargetingVoxelSurface = VoxelPosHelper.WorldPosIsOnVoxelSurface(hitInfo.point);
                if(VoxelPosHelper.WorldPosIsOnVoxelSurface(hitInfo.point))
                {
                    // If the ray hits right on the grid border between two voxels, the normal determines
                    // which voxel will be targeted
                    var normalDirection = surfaceVoxel ? 0.5f : -0.5f;
                    var voxelCenterWorldPos = hitInfo.point + hitInfo.normal * normalDirection;
                    voxelPos = VoxelPosHelper.GetVoxelPosFromWorldPos(voxelCenterWorldPos);                    
                }
                else
                {
                    // Otherwise if our hitpoint is inside a voxel, there is no ambiguity
                    voxelPos = VoxelPosHelper.GetVoxelPosFromWorldPos(hitInfo.point);
                }

                _debugLastTargetVoxel = new Vector3(voxelPos.x, voxelPos.y, voxelPos.z);

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

    private PlayerActionBarController _actionBar;

    private Vector3 _velocity;

    private WorldGenerator _worldGen;

    private Ray? _debugLastRay;

    private Vector3? _debugLastHit;

    private Vector3? _debugLastTargetVoxel;

    private Vector3? _lastPlacedVoxel;

    private Vector3? _debugLastHitNormal;

    private bool _debugTargetingVoxelSurface;

    void OnDrawGizmos()
    {
        //if(_lastPlacedVoxel != null) Gizmos.DrawCube(_lastPlacedVoxel.Value, Vector3.one);
        if(_debugLastRay != null) Gizmos.DrawRay(_debugLastRay.Value);
        if(_debugLastHit != null) 
        {
            Gizmos.DrawSphere(_debugLastHit.Value, .075f);
            if(_debugLastHitNormal != null)
            {
                Gizmos.DrawRay(_debugLastHit.Value, _debugLastHitNormal.Value);
            }
        }
        if(_debugLastTargetVoxel != null) Gizmos.DrawWireCube(_debugLastTargetVoxel.Value + Vector3.one * 0.5f, Vector3.one);
    }
}
