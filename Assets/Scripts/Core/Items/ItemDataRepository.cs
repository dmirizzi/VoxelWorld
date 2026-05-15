using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public static class ItemDataRepository
{
    static ItemDataRepository()
    {
        var itemDataContent = Resources.Load<TextAsset>("ItemData").text;
        var items = JsonConvert.DeserializeObject<ItemTypeList>(itemDataContent).Items;

        _itemsByName = new Dictionary<string, ItemData>();
        foreach(var item in items)
        {
            _itemsByName[item.Name] = item;
        }
    }

    public static bool TryGetItemData(string name, out ItemData item)
    {
        if(!_itemsByName.ContainsKey(name))
        {
            item = null;
            return false;
        }

        item = _itemsByName[name];
        return true;
    }

    public static ItemData GetItemData(string name)
    {
        return _itemsByName[name];
    }

    private class ItemTypeList
    {
        public List<ItemData> Items; 
    }

    private static Dictionary<string, ItemData> _itemsByName;
}
