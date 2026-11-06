using HarmonyLib;

namespace Oddhatter.AlwaysEitr
{
    /// <summary>
    /// The always-on pool. Player.UpdateFood (every second, and on load, respawn and eating) sums food values here
    /// and passes the eitr total to SetMaxEitr. That is the only source of max eitr, so adding BaseEitr to the total
    /// stacks the pool under food: 200 pool + 100 food = 300, back to 200 when the food wears off.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.GetTotalFoodValue))]
    internal static class Player_GetTotalFoodValue_Patch
    {
        private static void Postfix(ref float eitr)
        {
            if (AlwaysEitrPlugin.Enabled.Value)
            {
                eitr += AlwaysEitrPlugin.BaseEitr.Value;
            }
        }
    }

    /// <summary>
    /// EitrRegenMultiplier. Player.UpdateStats(float) computes regen from m_eiterRegen (then applies status effect and
    /// gear modifiers). Scale the base rate for that call only and always restore it, so the prefab value is never
    /// changed and can't compound.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.UpdateStats), new[] { typeof(float) })]
    internal static class Player_UpdateStats_Patch
    {
        private static void Prefix(Player __instance, out float __state)
        {
            __state = __instance.m_eiterRegen;
            if (AlwaysEitrPlugin.Enabled.Value)
            {
                __instance.m_eiterRegen *= AlwaysEitrPlugin.EitrRegenMultiplier.Value;
            }
        }

        // A finalizer runs even if UpdateStats throws, unlike a postfix.
        private static void Finalizer(Player __instance, float __state)
        {
            __instance.m_eiterRegen = __state;
        }
    }
}
