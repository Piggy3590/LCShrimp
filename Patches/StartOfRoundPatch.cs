using HarmonyLib;
using UnityEngine;
using Unity.Netcode;

namespace Shrimp.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    internal class StartOfRoundPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch("Start")]
        private static void Start_Prefix(GrabbableObject __instance)
        {
            GameObject shrimpItemManager = GameObject.Instantiate(Plugin.shrimpItemManager);
            shrimpItemManager.GetComponent<NetworkObject>().Spawn();
        }
    }
}
