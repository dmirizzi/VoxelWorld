using UnityEngine;

public interface IPlayerHoldable
{
    Vector3 HoldingOffset { get; }

    Quaternion HoldingRotation { get; }

    void OnHold(GameObject playerObject);

    void OnRemove(GameObject playerObject);
}
