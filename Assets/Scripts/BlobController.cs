using UnityEngine;

public class BlobController : MonoBehaviour
{
    public enum BlobGoalState
    {
        Idle,

        Exploring
    }

    public enum BlobActionState
    {
        Idle,
        FindingDestination,
        Turning,
        Jumping
    }

    [field: SerializeField]
    public Vector3? Destination { get; private set; }

    [field: SerializeField]
    public BlobGoalState CurrentGoalState { get; private set; }

    [field: SerializeField]
    public BlobActionState CurrentActionState { get; private set; }

    private WorldGenerator _worldGen;

    private Vector3 _jumpOffPoint;

    // Start is called before the first frame update
    void Start()
    {
        _worldGen = GameObject.FindObjectsOfType<WorldGenerator>()[0];
    }

    // Update is called once per frame
    void Update()
    {
        UpdateState();
        ExecuteState();
    }

    private void ExecuteState()
    {
        switch(CurrentGoalState)
        {
            case BlobGoalState.Exploring:
                if(Destination == null)
                {
                    CurrentActionState = BlobActionState.FindingDestination;
                    TargetRandomNeighboringVoxel();
                }

                if(Destination != null)
                {
                    if(CurrentActionState == BlobActionState.FindingDestination)
                    {
                        CurrentActionState = BlobActionState.Turning;
                        if(Destination != null) Debug.Log($"Facing towards {Destination.Value}");

                    }

                    if(CurrentActionState == BlobActionState.Turning)
                    {
                        if(FaceTowardsDestination())
                        {
                            _jumpOffPoint = transform.position;
                            CurrentActionState = BlobActionState.Jumping;
                            Debug.Log($"Jumping towards destination {Destination.Value}");
                        }
                    }

                    if(CurrentActionState == BlobActionState.Jumping)
                    {
                        if(JumpTowardsTarget())
                        {
                            Debug.Log($"Reached destination {Destination.Value}");
                            CurrentActionState = BlobActionState.Idle;
                            Destination = null;
                        }
                    }
                }
            break;
        }
    }

    private bool JumpTowardsTarget()
    {
        var totalGroundDeltaToTarget = Destination.Value - _jumpOffPoint;
        totalGroundDeltaToTarget.y = 0;

        var currentGroundDeltaToTarget = Destination.Value - transform.position;
        currentGroundDeltaToTarget.y = 0;

        var normalizedDistanceToTarget = currentGroundDeltaToTarget.magnitude / totalGroundDeltaToTarget.magnitude;

        if(normalizedDistanceToTarget < 0.01f)
        {
            transform.position = Destination.Value;
            return true;
        }

        transform.position = new Vector3(
            transform.position.x + totalGroundDeltaToTarget.x  * Time.deltaTime,
            _jumpOffPoint.y + Mathf.Sin(normalizedDistanceToTarget * 180 * Mathf.Deg2Rad),
            transform.position.z + totalGroundDeltaToTarget.z * Time.deltaTime
        );

        return false;
    }

    private bool FaceTowardsDestination()
    {
        var targetYAngle = Quaternion.LookRotation(
            Destination.Value - transform.position,
            transform.up
        ).eulerAngles.y;

        var targetRotation = Quaternion.Euler(
            transform.rotation.eulerAngles.x,
            targetYAngle,
            transform.rotation.eulerAngles.z
        );

        if(Quaternion.Angle(transform.rotation, targetRotation) < 1f)
        {
            transform.rotation = targetRotation;
            return true;
        }

        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 2f);
        return false;
    }

    private void TargetRandomNeighboringVoxel()
    {
        var randomOffset = new Vector3Int(Random.Range(-1, 2), 0, Random.Range(-1, 2));
        if(randomOffset.x == 0 && randomOffset.z == 0)
        {
            return;
        }

        Debug.Log($"Current Voxel Pos = {VoxelPosConverter.GetVoxelPosFromWorldPos(transform.position)}");

        var voxelPos = VoxelPosConverter.GetVoxelPosFromWorldPos(transform.position) + randomOffset;

        var y = _worldGen.VoxelWorld.GetHighestVoxelPos(voxelPos.x, voxelPos.z);

        Debug.Log($"Highest voxel @ {voxelPos} -> {(y.HasValue ? y.Value : -123)}");

        if(y.HasValue)
        {
            Destination = VoxelPosConverter.GetVoxelTopCenterSurfaceWorldPos(new Vector3Int(
                voxelPos.x,
                y.Value,
                voxelPos.z
            )) + (Vector3.up * GetComponent<Renderer>().bounds.size.y / 2);
        }
    }

    private void UpdateState()
    {
        if(CurrentActionState != BlobActionState.Idle)
        {
            return;
        }

        switch(CurrentGoalState)
        {
            case BlobGoalState.Idle:
            case BlobGoalState.Exploring:
                CurrentGoalState = BlobGoalState.Exploring;
                break;
        }
    }
}
