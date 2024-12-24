using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Shrimp.Patches
{
    public class ShrimpItemManager : NetworkBehaviour
    {
        public static ShrimpItemManager Instance { get; private set; }
        public List<GrabbableObject> droppedItems = new List<GrabbableObject>();
        public List<GameObject> droppedObjects = new List<GameObject>();

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            
            else if (Instance != this)
                Destroy(gameObject);
            
            DontDestroyOnLoad(gameObject);
        }
    }
}

