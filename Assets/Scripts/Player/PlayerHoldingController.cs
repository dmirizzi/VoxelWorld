using System.Collections.Generic;
using UnityEngine;

public class PlayerHoldingController : MonoBehaviour
{
    public GameObject HoldingGameObject;

    public IPlayerHoldable PlayerHoldeable;

    void Start()
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
            throw new System.Exception($"Object {obj.name} is not holdeable!");
        }

        HoldingGameObject = obj;

        // Set GameObject and all its child objects to the "PlayerHeldItem" layer
        // so they will only be drawn on the overlay camera
        SetHoldingGameObjectLayerToOverlay();

        // Attach holding object to camera
        HoldingGameObject.transform.parent = _cameraTransform;
        HoldingGameObject.transform.localPosition = 
                _cameraTransform.right * PlayerHoldeable.HoldingOffset.x +
                _cameraTransform.up * PlayerHoldeable.HoldingOffset.y +
                _cameraTransform.forward * PlayerHoldeable.HoldingOffset.z;
        HoldingGameObject.transform.localRotation = PlayerHoldeable.HoldingRotation;

        PlayerHoldeable.OnHold(gameObject);
    }

    public void RemoveHeldObject()
    {
        if(PlayerHoldeable != null && HoldingGameObject != null)
        {
            PlayerHoldeable.OnRemove(gameObject);
            RestoreHoldingGameObjectLayers();

            PlayerHoldeable = null;
            HoldingGameObject = null;

            HoldingGameObject.transform.parent = null;
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
