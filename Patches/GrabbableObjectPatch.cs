using HarmonyLib;

namespace Shrimp.Patches
{
    [HarmonyPatch(typeof(GrabbableObject))]
    internal class GrabbableObjectPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        private static void Start_Postfix(GrabbableObject __instance)
        {
            if (__instance.gameObject.GetComponent<ItemGrabChecker>() == null)
            {
                __instance.gameObject.AddComponent<ItemGrabChecker>();
            }
        }
    }
}
