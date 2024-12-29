using System.Collections;
using System.Collections.Generic;
using System.Text;
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
        ActionBarItems[2] = ItemDataRepository.GetItemData("Cobblestone");
        ActionBarItems[3] = ItemDataRepository.GetItemData("CobblestoneWedge");
        ActionBarItems[4] = ItemDataRepository.GetItemData("Door");
        ActionBarItems[5] = ItemDataRepository.GetItemData("Ladder");
        ActionBarItems[6] = ItemDataRepository.GetItemData("YellowLightblock");
        ActionBarItems[7] = ItemDataRepository.GetItemData("RedLightblock");
        ActionBarItems[8] = ItemDataRepository.GetItemData("BlueLightblock");

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
        var sb = new StringBuilder();
        for(int i = 0; i < ActionBarItems.Length; ++i)
        {
            var name = ActionBarItems[i] != null ? ActionBarItems[i].Name : "N/A";
            var txt = $"{i + 1} - {name}";
            if(ActionBarItems[i] != null && CurrentlySelectedItem == ActionBarItems[i])
            {
                txt = $"[[{txt}]]";
            }
            sb.Append(txt);
            sb.Append("  ");
        }
        
        GUI.Label(new Rect(10, 30, 1500, 20), sb.ToString());
    }

    private GameObject _lastEquippedGameObject;

    private PlayerHoldingController _playerHoldingController;

}
