using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TVEngine.Ui
{

    /// <summary>
    /// Handles wrequests when an item that has been added/removed from the inventory, 
    /// only 6 requests can be queued at a time any additional ones before the queue is not max size is ignored
    /// </summary>
    public class HudLootDisplayController : MonoBehaviour
    {

        public struct ItemData
        {
            public ItemDictionary.ItemIDs ItemId;
            public int Amount;

            public ItemData(ItemDictionary.ItemIDs pItemId, int pAmount)
            {
                ItemId = pItemId;
                Amount = pAmount;
            }
        }

        [Header("Lookup Reference")]
        [SerializeField]
        private ItemDictionary _itemDictionary;

        [SerializeField]
        private UILootBaseSlot[] _uiLootSlots;

        [Header("Slot Settings")]
        [SerializeField]
        private AnimationCurve _slideInCurve;

        private Queue<ItemData> _lootToDisplay = new Queue<ItemData>();
        private int _nextFreeSlot = 0;

        private Coroutine _tickSlotsRoutine = null;

        private void OnEnable()
        {
            _tickSlotsRoutine = StartCoroutine(TickSlotsRoutine());
        }
        public void QueueItem(ItemDictionary.ItemIDs pItem, int pAmount)
        {
            bool firstElement = false;

            if (_lootToDisplay.Count == 0)
            {
                firstElement = true;
            }

            if (_lootToDisplay.Count >= _uiLootSlots.Length)
            {
#if UNITY_EDITOR
                Debug.Log("Item Queue is full, ignoring request!");
#endif
                return;
            }

            _lootToDisplay.Enqueue(new ItemData(pItem, pAmount));

            if (firstElement)
            {
                gameObject.SetActive(true);
            }

            ItemDataStruct itemData = _itemDictionary.GetItemDataWithUniqueID(pItem);
            _uiLootSlots[_nextFreeSlot].Init(itemData.InventoryIcon, GameController.GetStringReflectionID(itemData.ItemNameID), pAmount, _slideInCurve);


            _nextFreeSlot++;

            if (_nextFreeSlot >= _uiLootSlots.Length)
            {
                _nextFreeSlot = 0;
            }

        }

        public void DequeueItem()
        {
            _lootToDisplay.Dequeue();

            if (_lootToDisplay.Count == 0)
            {
                if (_tickSlotsRoutine != null)
                {
                    StopCoroutine(_tickSlotsRoutine);
                    _tickSlotsRoutine = null;
                }
                gameObject.SetActive(false);
                _nextFreeSlot = 0;
            }
        }
        private IEnumerator TickSlotsRoutine()
        {
            while (_lootToDisplay.Count > 0)
            {
                for (int i = 0; i < _uiLootSlots.Length; i++)
                {
                    if (_uiLootSlots[i].Tick(Time.deltaTime))
                    {
                        DequeueItem();
                    }
                }
                yield return new WaitForFixedUpdate();
            }
        }
    }
}
