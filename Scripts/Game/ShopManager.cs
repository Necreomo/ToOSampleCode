using System.Collections.Generic;
using TVEngine.Inventory;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    [SerializeField]
    private TrinketDictionary _trinketDictionary;

    [SerializeField]
    private DialogChain _shopDialog;

    [Tooltip("Trinkets available at the shop")]
    [SerializeField]
    private TrinketDictionary.TrinketIDs[] _shopTrinkets;

    [SerializeField]
    private Transform _shopItemPlacementRoot;

    private List<InteractiveShopItem> _inWorldTrinkets = new List<InteractiveShopItem>();
    private int _currentShopItemIndex = int.MaxValue;
    // Start is called before the first frame update
    void Start()
    {
        for (int i = 0; i < _shopItemPlacementRoot.childCount; i++)
        {
            if (i >= _shopTrinkets.Length)
            {
                break;
            }
            else
            {
                InteractiveShopItem interactiveShopItem =
                Instantiate(_trinketDictionary.GetTrinketPrefabWithUniqueID(_shopTrinkets[i]), 
                    _shopItemPlacementRoot.GetChild(i).position,
                    _shopItemPlacementRoot.GetChild(i).rotation,
                    _shopItemPlacementRoot.GetChild(i)).GetComponent<InteractiveShopItem>();
                interactiveShopItem.Initialize(pStoreIndex: i, ShopItemInteract);
                _inWorldTrinkets.Add(interactiveShopItem);
            }
        }
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (PersistantPlayerData.Instance == null)
        {
            Debug.LogError($"The ShopManager null exception error can be ignored if this scene was loaded directly without going through the main menu");
        }
#endif
        PersistantPlayerData.Instance.OnInitialzeStoreDelegate += InitializeStoreBasedOnInventory;
    }

    private void OnDisable()
    {
        PersistantPlayerData.Instance.OnInitialzeStoreDelegate -= InitializeStoreBasedOnInventory;
    }

    private void InitializeStoreBasedOnInventory(InventoryManager pInventoryManager, bool pRespawn)
    {
        if (pRespawn)
        {
            RepopulateShopToInitialState();
        }

        for (int i = 0; i < _shopTrinkets.Length; i++)
        {
            if (_shopTrinkets[i] != TrinketDictionary.TrinketIDs.None || _shopTrinkets[i] != TrinketDictionary.TrinketIDs.Count)
            {
                if (pInventoryManager.HasTrinketUnlocked((int)_shopTrinkets[i], _trinketDictionary.IsTrinketSpecialWithUniqueID(_shopTrinkets[i])))
                {
                    //Remove game object from store
                    if (_inWorldTrinkets[i] != null)
                    {
                        _inWorldTrinkets[i].OnPurchaseSuccess();
                        _inWorldTrinkets[i] = null;
                    }
                }
            }
        }
    }

    private void ShopItemInteract(int pStoreIndex)
    {
        _currentShopItemIndex = pStoreIndex;
        GameController.Instance.StartDialogChain(_trinketDictionary.GetTrinketPreShopDialogWithUniqueID(_shopTrinkets[_currentShopItemIndex]), pDialogEndCallback: OnPreShopItemDialogComplete);
    }

    private void OnPreShopItemDialogComplete()
    {
        GameController.Instance.StartShopDialog(this, _shopDialog,
            _trinketDictionary.GetTrinketDataWithUniqueID(_shopTrinkets[_currentShopItemIndex]),
            pDialogEndCallback: GameController.Instance.OnShopDialogClose);
    }

    public void OnShopDialogClose(bool pPurchased, int pUnequippedTrinket = int.MaxValue)
    {
        if (pPurchased)
        {
            if (_currentShopItemIndex >= 0 && 
                _currentShopItemIndex < _shopTrinkets.Length && 
                _inWorldTrinkets[_currentShopItemIndex] != null)
            {
                GameController.Instance.OnShopTrinketPurchase(_shopTrinkets[_currentShopItemIndex],
                    _trinketDictionary.IsTrinketSpecialWithUniqueID(_shopTrinkets[_currentShopItemIndex]));

                _inWorldTrinkets[_currentShopItemIndex].OnPurchaseSuccess();
                _inWorldTrinkets[_currentShopItemIndex] = null;

            }
        }
        _currentShopItemIndex = int.MaxValue;
    }

    /// <summary>
    /// Used to repopulate missing items from the shop
    /// </summary>
    private void RepopulateShopToInitialState()
    {
        for (int i = 0; i < _shopTrinkets.Length; i++)
        {
            if (_inWorldTrinkets[i] == null)
            {
                InteractiveShopItem interactiveShopItem =
                    Instantiate(_trinketDictionary.GetTrinketPrefabWithUniqueID(_shopTrinkets[i]),
                        _shopItemPlacementRoot.GetChild(i).position,
                        _shopItemPlacementRoot.GetChild(i).rotation,
                        _shopItemPlacementRoot.GetChild(i)).GetComponent<InteractiveShopItem>();
                        interactiveShopItem.Initialize(pStoreIndex: i, ShopItemInteract);
                        _inWorldTrinkets[i] = interactiveShopItem;
            }
        }
    }

}
