using UnityEngine;

public interface IPlayerHoldable
{
    Vector3 HoldingOffset { get; }

    Quaternion HoldingRotation { get; }

    void OnHold(PlayerController player);

    void OnRemove(PlayerController player);
}
