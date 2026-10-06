using HarmonyLib;
using UnityEngine;

namespace ValheimGatherer
{
    [HarmonyPatch(typeof(ZNetView), "Awake")]
    internal static class ZNetViewAwakePatch
    {
        // Every networked world object (rocks, bushes, pickables, ...) passes through here when its zone loads.
        private static void Postfix(ZNetView __instance)
        {
            var zdo = __instance.GetZDO();
            if (zdo == null)
                return; // placement ghosts and other non-networked instances

            Tracker.Register(__instance, ResourceRules.MatchPrefab(zdo.GetPrefab(), __instance.gameObject));
        }
    }

    [HarmonyPatch(typeof(Location), "Awake")]
    internal static class LocationAwakePatch
    {
        // Locations (tar pits, ...) and the non-networked lore stones / vegvisirs placed inside them.
        private static void Postfix(Location __instance)
        {
            Tracker.Register(__instance, ResourceRules.MatchLocation(Utils.GetPrefabName(__instance.gameObject)));

            foreach (var stone in __instance.GetComponentsInChildren<RuneStone>(true))
                if (!stone.GetComponent<ZNetView>())
                    Tracker.Register(stone, ResourceRules.RuneStone);

            foreach (var vegvisir in __instance.GetComponentsInChildren<Vegvisir>(true))
                if (!vegvisir.GetComponent<ZNetView>())
                    Tracker.Register(vegvisir, ResourceRules.Vegvisir);
        }
    }

    [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Destroy))]
    internal static class ZNetSceneDestroyPatch
    {
        private static void Prefix(GameObject go)
        {
            if (go)
                Tracker.OnDestroyed(go);
        }
    }
}
