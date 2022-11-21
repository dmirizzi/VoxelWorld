using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Unity Event Methods
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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
        
        //TODO: Maybe a better approach would be to use colliders for custom blocks?
        //TODO: - Determine potentially touched voxels (via full block size)
        //TODO: - If not a custom block -> just simple box collision
        //TODO: - If custom block -> either get collider from BlockType or place colliders in the chunk at build?
        HandleTouchedVoxels();
        _controller.Move(_velocity * Time.deltaTime);
    } 
    void OnGUI()
    {
        var targetedVoxel = GetTargetedVoxelPos(false);
        var targetedVoxelType = targetedVoxel.Item1.HasValue ? _worldGen?.VoxelWorld.GetVoxel(targetedVoxel.Item1.Value) : null;
        
        GUI.Label(new Rect(10, 10, 1500, 18), $"LookDir={GetLookDir()} | TargetedVoxelType: {targetedVoxelType}");
    }

    void OnDrawGizmos()
    {
        if(_dbgLastRay != null) Gizmos.DrawRay(_dbgLastRay.Value);
        if(_dbgLastHit != null) 
        {
            Gizmos.DrawSphere(_dbgLastHit.Value, .075f);
            if(_dbgLastHitNormal != null)
            {
                Gizmos.DrawRay(_dbgLastHit.Value, _dbgLastHitNormal.Value);
            }
        }
        if(_dbgLastTargetVoxel != null) Gizmos.DrawWireCube(_dbgLastTargetVoxel.Value + Vector3.one * 0.5f, Vector3.one);

        foreach(var point in _dbgCollisionPoints)
        {
            Gizmos.DrawCube(point + Vector3.one * 0.5f, Vector3.one);
        }

        foreach(var ray in _dbgCollisionRays)
        {
            Gizmos.DrawRay(ray.Item1, ray.Item2);
        }

        if(_dbgGroundingSphere.Item1 != null)
        {
            Gizmos.DrawSphere(_dbgGroundingSphere.Item1, _dbgGroundingSphere.Item2);
        }
    }


    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // External access to player control
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void UpdateClimbingCounter(int delta)
    {
        _climbingCounter += delta;
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Voxel-player interactions
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void HandleTouchedVoxels()
    {
        var currentTouchedVoxelPositions = GetTouchedVoxelPositions();
        _dbgCollisionPoints = currentTouchedVoxelPositions.Select( x => (Vector3)x).ToList();

        var addedTouchedVoxelPositions = currentTouchedVoxelPositions.Except(_lastTouchedVoxels);
        foreach(var addedVoxelPos in addedTouchedVoxelPositions)
        {
            var voxel = _worldGen.VoxelWorld.GetVoxel(addedVoxelPos);
            if(voxel != 0)
            {
                var blockType = BlockTypeRegistry.GetBlockType(voxel);
                if(blockType != null)
                {
                    blockType.OnTouchStart(_worldGen.VoxelWorld, addedVoxelPos, this);
                }
            }
        }

        var removedTouchedVoxelPositions = _lastTouchedVoxels.Except(currentTouchedVoxelPositions);
        foreach(var removedVoxelPos in removedTouchedVoxelPositions)
        {
            var voxel = _worldGen.VoxelWorld.GetVoxel(removedVoxelPos);
            if(voxel != 0)
            {
                var blockType = BlockTypeRegistry.GetBlockType(voxel);
                if(blockType != null)
                {
                    blockType.OnTouchEnd(_worldGen.VoxelWorld, removedVoxelPos, this);
                }
            }
        }

        _lastTouchedVoxels = currentTouchedVoxelPositions;
    }

    private HashSet<Vector3Int> GetTouchedVoxelPositions()
    {                
        _dbgCollisionRays = new List<(Vector3, Vector3)>();

        int numRays = 8;
        float anglePerRay = 360f / numRays;

        var voxels = new HashSet<Vector3Int>();

        var position = transform.position + Vector3.up * _controller.height / 4;

        for(int y = 0; y <= 1; ++y )
        {
            for(float angle = 0f; angle <= 360f; angle += anglePerRay)
            {
                var direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;

/*
                var hitSomething = Physics.Raycast(
                    position,
                    direction,
                    out var hit,
                    _controller.radius + 0.1f,
                    LayerMask.GetMask("Voxels"),
                    QueryTriggerInteraction.Collide

*/            
                var hitSomething = Physics.SphereCast(
                    position,
                    .2f,
                    direction,
                    out var hit,
                    _controller.radius,
                    LayerMask.GetMask("Voxels"),
                    QueryTriggerInteraction.Collide
                );

                if(hitSomething)
                {
                    voxels.Add(GetVoxelPosFromWorldPos(hit.point, hit.normal, false));
                }

                _dbgCollisionRays.Add((position, direction));
            }

            position += Vector3.down * _controller.height / 2;
        }

        return voxels;
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
                    world.SetVoxel(voxelPos.Item1.Value, 0);
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

                    world.SetVoxel(
                        targetVoxelPos, 
                        BlockDataRepository.GetBlockTypeId(blockType), 
                        placementDir,
                        GetLookDir());
                }                   
            }
        }
    }

    private Vector3Int GetVoxelPosFromWorldPos(Vector3 point, Vector3 normal, bool surfaceVoxel)
    {
        if(VoxelPosHelper.WorldPosIsOnVoxelSurface(point))
        {
            // If the ray hits right on the grid border between two voxels, the normal determines
            // which voxel will be targeted
            var normalDirection = surfaceVoxel ? 0.5f : -0.5f;
            var voxelCenterWorldPos = point + normal * normalDirection;
            return VoxelPosHelper.GetVoxelPosFromWorldPos(voxelCenterWorldPos);                    
        }
        else
        {
            // Otherwise if our hitpoint is inside a voxel, there is no ambiguity
            var voxelPos = VoxelPosHelper.GetVoxelPosFromWorldPos(point);
            if(surfaceVoxel)
            {
                voxelPos += Vector3Int.FloorToInt(normal.normalized);
            }
            return voxelPos;
        }
    }

    private (Vector3Int?, BlockFace?) GetTargetedVoxelPos(bool surfaceVoxel)
    {
        _dbgLastRay = new Ray(CameraTransform.position, CameraTransform.forward);
        _dbgLastHit = null;

        Vector3Int voxelPos = Vector3Int.zero;
        Vector3 normal = Vector3.zero;
        Vector3 hitPos = Vector3.zero;

        // First look if there is a custom voxel collider and get the targeted voxel position from that
        if(Physics.Raycast(
            CameraTransform.position, 
            CameraTransform.forward, 
            out var hitInfoCollider,
            MaxInteractionDistance,
            LayerMask.GetMask("VoxelColliders")))
        {
            if(hitInfoCollider.distance <= MaxInteractionDistance)
            {
                normal = hitInfoCollider.normal.normalized;
                hitPos = hitInfoCollider.point;

                var voxelCollider = hitInfoCollider.transform.gameObject.GetComponent<VoxelCollider>();
                if(voxelCollider != null)
                {
                    voxelPos = voxelCollider.GlobalVoxelPos;                    
                }
                else
                {
                    UnityEngine.Debug.LogError($"Voxel collider game object {hitInfoCollider.transform} is missing VoxelCollider script!");
                }
            }
        }        

        // If not, determine the targeted voxel collision from the hit on the chunk mesh
        else if(Physics.Raycast(
            CameraTransform.position, 
            CameraTransform.forward, 
            out var hitInfoVoxel,
            MaxInteractionDistance,
            LayerMask.GetMask("Voxels")))
        {
            if(hitInfoVoxel.distance <= MaxInteractionDistance)
            {
                voxelPos = GetVoxelPosFromWorldPos(hitInfoVoxel.point, hitInfoVoxel.normal, surfaceVoxel);
                normal = hitInfoVoxel.normal.normalized;
                hitPos = hitInfoVoxel.point;                
            }
        }

        var voxelFace = BlockFaceHelper.GetBlockFaceFromVector(normal);
        if(voxelFace.HasValue)
        {
            voxelFace = BlockFaceHelper.GetOppositeFace(voxelFace.Value);
        }
        else
        {
            voxelFace = BlockFaceHelper.GetBlockFaceFromVector(GetClosestCardinalLookDirection());
        }

        _dbgLastHit = hitPos;
        _dbgLastHitNormal = normal;
        _dbgTargetingVoxelSurface = VoxelPosHelper.WorldPosIsOnVoxelSurface(hitPos);               
        _dbgLastTargetVoxel = new Vector3(voxelPos.x, voxelPos.y, voxelPos.z);

        return (voxelPos, voxelFace);
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

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Movement
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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
        var movementY = (_climbingCounter > 0) ? CameraTransform.forward.y : 0;

        var forwardXZ = new Vector3(CameraTransform.forward.x, movementY, CameraTransform.forward.z).normalized;
        var rightXZ = new Vector3(CameraTransform.right.x, movementY, CameraTransform.right.z).normalized;
        _controller.Move((forwardXZ * forward + rightXZ * right) * WalkingSpeed * Time.deltaTime);
    }

    private void HandleJumping()
    {
        if(IsGroundedForJumping() && Input.GetButtonDown("Jump"))
        {
            _velocity = Vector3.up * JumpForce;
        }
        else if(_climbingCounter == 0 && !IsGroundedForGravity())
        {
            // Freefall
            _velocity += Physics.gravity * Time.deltaTime * GravityFactor;
        }
        else
        {
            // Climbing
            _velocity = Vector3.zero;
        }
    }

    private (Vector3, float) _dbgGroundingSphere;

    private bool IsGroundedForJumping()
    {
        var sphereRadius = _controller.radius * .75f;

        var spherePos = (transform.position + Vector3.down * (_controller.height / 2 - sphereRadius));
        _dbgGroundingSphere = (spherePos, sphereRadius);

        return Physics.SphereCast(
            spherePos, 
            sphereRadius,
            Vector3.down,
            out var hitInfo,
            sphereRadius,
            LayerMask.GetMask("Voxels"));
    }

    private bool IsGroundedForGravity()
    {
        var distanceToPlayerBottom = _controller.bounds.size.y / 2f;
        return Physics.Raycast(
            transform.position, 
            Vector3.down, 
            distanceToPlayerBottom + 0.1f,
            LayerMask.GetMask("Voxels"));
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Private attributes
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    private CharacterController _controller;

    private PlayerActionBarController _actionBar;

    private Vector3 _velocity;

    private WorldGenerator _worldGen;

    private HashSet<Vector3Int> _lastTouchedVoxels = new HashSet<Vector3Int>();

    private int _climbingCounter;

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Debug Gizmo Stuff
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private Ray? _dbgLastRay;

    private Vector3? _dbgLastHit;

    private Vector3? _dbgLastTargetVoxel;

    private Vector3? _dbgLastHitNormal;

    private bool _dbgTargetingVoxelSurface;

    private List<(Vector3, Vector3)> _dbgCollisionRays = new List<(Vector3, Vector3)>();

    private List<Vector3> _dbgCollisionPoints = new List<Vector3>();
}
