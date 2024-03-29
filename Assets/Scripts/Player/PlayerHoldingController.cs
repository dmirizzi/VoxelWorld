using System.Collections.Generic;
using UnityEngine;

public class PlayerHoldingController : MonoBehaviour
{
    public GameObject HoldingGameObject;

    public IPlayerHoldable PlayerHoldeable;

    void Awake()
    {
        _cameraTransform = GameObject.Find("Main Camera").transform;
        _layerBackup = new Dictionary<GameObject, int>();
    }

    public void HoldObject(GameObject obj)
    {
        if(HoldingGameObject != null)
        {
            RemoveHeldObject();
        }

        PlayerHoldeable = obj.GetComponent<IPlayerHoldable>();
        if(PlayerHoldeable == null)
        {
            Debug.LogError($"GameObject {obj.name} is not holdeable");
        }

        HoldingGameObject = obj;

        // Set GameObject and all its child objects to the "PlayerHeldItem" layer
        // so they will only be drawn on the overlay camera
        SetHoldingGameObjectLayerToOverlay();

        // Attach holding object to camera
        HoldingGameObject.transform.position = 
                _cameraTransform.position +
                _cameraTransform.right * PlayerHoldeable.HoldingOffset.x +
                _cameraTransform.up * PlayerHoldeable.HoldingOffset.y +
                _cameraTransform.forward * PlayerHoldeable.HoldingOffset.z;
        HoldingGameObject.transform.rotation = _cameraTransform.rotation * PlayerHoldeable.HoldingRotation;
        HoldingGameObject.transform.parent = _cameraTransform;

        PlayerHoldeable.OnHold(GetComponent<PlayerController>());
    }

    public void RemoveHeldObject()
    {
        if(PlayerHoldeable != null && HoldingGameObject != null)
        {
            PlayerHoldeable.OnRemove(GetComponent<PlayerController>());
            RestoreHoldingGameObjectLayers();

            HoldingGameObject.transform.parent = null;

            PlayerHoldeable = null;
            HoldingGameObject = null;

        }
    }

    private void SetHoldingGameObjectLayerToOverlay()
    {
        HoldingGameObject.layer = LayerMask.NameToLayer("PlayerHeldItem");
        _layerBackup[HoldingGameObject] = HoldingGameObject.layer;
        foreach(var trans in HoldingGameObject.GetComponentsInChildren<Transform>(true))
        {
            _layerBackup[trans.gameObject] = trans.gameObject.layer;
            trans.gameObject.layer = LayerMask.NameToLayer("PlayerHeldItem");
        }
    }

    private void RestoreHoldingGameObjectLayers()
    {
        HoldingGameObject.layer = _layerBackup[HoldingGameObject];
        foreach(var trans in HoldingGameObject.GetComponentsInChildren<Transform>(true))
        {
            trans.gameObject.layer = _layerBackup[trans.gameObject];
        }
        _layerBackup.Clear();
    }

    private Dictionary<GameObject, int> _layerBackup;

    private Transform _cameraTransform;
}
