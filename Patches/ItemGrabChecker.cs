using Unity.Netcode;
using UnityEngine;

namespace Shrimp.Patches
{
    public class ItemGrabChecker : NetworkBehaviour
    {
        public bool isInElevatorB;
        public bool droppedItemMoment;
        private bool droppedItemAppended;
        public float dogEatTimer;
        public bool isHeldBefore;

        public GrabbableObject grabbableObject;

        void Start()
        {
            grabbableObject = this.GetComponent<GrabbableObject>();
        }

        void Update()
        {
            if (grabbableObject.deactivated)
            {
                return;
            }
            if (grabbableObject.isHeld)
            {
                if (!isHeldBefore)
                {
                    isHeldBefore = true;
                }
                dogEatTimer = 0;
                droppedItemMoment = true;
                droppedItemAppended = false;
                ShrimpItemManager.Instance.droppedItems.Remove(grabbableObject);
                ShrimpItemManager.Instance.droppedObjects.Remove(gameObject);
            }
            else
            {
                if (droppedItemMoment && grabbableObject.reachedFloorTarget)
                {
                    if (!droppedItemAppended)
                    {
                        ShrimpItemManager.Instance.droppedItems.Add(grabbableObject);
                        ShrimpItemManager.Instance.droppedObjects.Add(gameObject);
                        droppedItemAppended = true;
                    }
                }
                dogEatTimer += Time.deltaTime;

                if (dogEatTimer > 3f)
                {
                    ShrimpItemManager.Instance.droppedItems.Remove(grabbableObject);
                    ShrimpItemManager.Instance.droppedObjects.Remove(gameObject);
                    dogEatTimer = 0;
                    droppedItemMoment = false;
                    droppedItemAppended = false;
                }
            }

        }

        void OnDestroy()
        {
            ShrimpItemManager.Instance.droppedItems.Remove(grabbableObject);
            ShrimpItemManager.Instance.droppedObjects.Remove(this.gameObject);
        }
    }
}