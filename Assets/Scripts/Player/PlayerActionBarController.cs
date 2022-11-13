using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerActionBarController : MonoBehaviour
{
    public ItemData CurrentlySelectedItem;

    public ItemData[] ActionBarItems;

    public void SelectActionBarSlot(int index)
    {       
        if(CurrentlySelectedItem == ActionBarItems[index])
        {
            return;
        }

        if(ActionBarItems[index] != null)
        {
            if(CurrentlySelectedItem != null)
            {
                _playerHoldingController.RemoveHeldObject();
                Destroy(_lastEquippedGameObject);
                _lastEquippedGameObject = null;
            }

            CurrentlySelectedItem = ActionBarItems[index];

            if(!string.IsNullOrEmpty(CurrentlySelectedItem.PrefabResource))
            {
                var prefab = Resources.Load<GameObject>(CurrentlySelectedItem.PrefabResource);
                _lastEquippedGameObject = Instantiate(prefab);
                _playerHoldingController.HoldObject(_lastEquippedGameObject);
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        _playerHoldingController = GetComponent<PlayerHoldingController>();

        ActionBarItems = new ItemData[10];
        ActionBarItems[0] = ItemDataRepository.GetItemData("Torch");
        ActionBarItems[1] = ItemDataRepository.GetItemData("Grass");
        ActionBarItems[2] = ItemDataRepository.GetItemData("Dirt");
        ActionBarItems[3] = ItemDataRepository.GetItemData("Cobblestone");
        ActionBarItems[4] = ItemDataRepository.GetItemData("CobblestoneWedge");
        ActionBarItems[5] = ItemDataRepository.GetItemData("Door");

        SelectActionBarSlot(0);
    }

    // Update is called once per frame
    void Update()
    {
        for(int i = 0; i < 10; ++i)
        {
            var key = (KeyCode)((int)KeyCode.Alpha1 + i);
            if(Input.GetKeyDown(key))
            {
                SelectActionBarSlot(i);
            }
        }
    }

    void OnGUI()
    {
        for(int i = 0; i < ActionBarItems.Length; ++i)
        {
            var name = ActionBarItems[i] != null ? ActionBarItems[i].Name : "N/A";
            var txt = $"{i + 1} - {name}";
            if(ActionBarItems[i] != null && CurrentlySelectedItem == ActionBarItems[i])
            {
                txt = $"[[{txt}]]";
            }
            GUI.Label(new Rect(10, 40 + i * 20, 200, 18), txt);
        }
    }

    private GameObject _lastEquippedGameObject;

    private PlayerHoldingController _playerHoldingController;

}
