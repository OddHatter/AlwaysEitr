using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Oddhatter.AlwaysEitr
{
    /// <summary>
    /// Per-staff summon limit. Vanilla limits summons twice, both on the caster's machine:
    ///  1. SpawnAbility.m_maxSpawned: a cast that finds that many of the creature already loaded spawns nothing.
    ///  2. ZDO MaxInstances (from the staff's skill level-ups or weapon quality): when a new summon starts following,
    ///     Tameable.UnsummonMaxInstances unsummons the oldest ones following the same player beyond that number.
    /// A staff's setting (when not 0) replaces both with the same number, whatever the Blood Magic level.
    /// Staffs are found at runtime (their data is in assets), so modded summoning staffs get a setting too.
    /// </summary>
    internal static class Summons
    {
        internal const string Section = "Summons";

        /// <summary>Staff item prefab name → its setting.</summary>
        private static readonly Dictionary<string, ConfigEntry<int>> s_byStaff = new Dictionary<string, ConfigEntry<int>>();

        /// <summary>Summoned creature prefab name → the settings of every staff that summons it.</summary>
        private static readonly Dictionary<string, List<ConfigEntry<int>>> s_byCreature = new Dictionary<string, List<ConfigEntry<int>>>();

        /// <summary>Setting names already used (two staffs with the same in-game name get their prefab name added).</summary>
        private static readonly HashSet<string> s_keys = new HashSet<string>();

        /// <summary>
        /// Finds summoning staffs in ObjectDB and binds a setting for each. Safe to call repeatedly. Only items with a
        /// crafting recipe count: ObjectDB also holds monster and boss attacks as items, and some of those summon too.
        /// </summary>
        internal static void Scan(ObjectDB db)
        {
            if (db == null || db.m_items == null)
            {
                return;
            }
            var craftable = new HashSet<string>();
            foreach (Recipe recipe in db.m_recipes)
            {
                if (recipe != null && recipe.m_item != null)
                {
                    craftable.Add(recipe.m_item.gameObject.name);
                }
            }
            foreach (GameObject itemPrefab in db.m_items)
            {
                ItemDrop itemDrop = itemPrefab != null ? itemPrefab.GetComponent<ItemDrop>() : null;
                if (itemDrop == null || !craftable.Contains(itemPrefab.name) || s_byStaff.ContainsKey(itemPrefab.name))
                {
                    continue;
                }
                ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData.m_shared;
                var spawns = new List<SpawnAbility>();
                AddSpawnAbilities(shared.m_attack, spawns);
                AddSpawnAbilities(shared.m_secondaryAttack, spawns);

                SpawnAbility summon = null;
                var creatures = new List<string>();
                foreach (SpawnAbility spawn in spawns)
                {
                    foreach (GameObject creature in spawn.m_spawnPrefab ?? new GameObject[0])
                    {
                        if (creature != null && creature.GetComponent<Character>() != null && !creatures.Contains(creature.name))
                        {
                            creatures.Add(creature.name);
                            summon = summon ?? spawn;
                        }
                    }
                }
                if (summon == null)
                {
                    continue;
                }

                string staffName = Localize(shared.m_name);
                string creatureName = Localize(summon.m_spawnPrefab[0].GetComponent<Character>().m_name);
                string key = SafeKey(staffName);
                if (key.Length == 0 || !s_keys.Add(key))
                {
                    key = SafeKey($"{staffName} ({itemPrefab.name})");
                    s_keys.Add(key);
                }
                ConfigEntry<int> entry = AlwaysEitrPlugin.Cfg.Bind(Section, key, 0,
                    new ConfigDescription(
                        $"Most {creatureName} summons from the {staffName} you can have out at once. " +
                        $"0 = the normal game limit ({NormalLimit(summon)}).",
                        new AcceptableValueRange<int>(0, 50)));
                s_byStaff[itemPrefab.name] = entry;
                foreach (string creature in creatures)
                {
                    if (!s_byCreature.TryGetValue(creature, out List<ConfigEntry<int>> list))
                    {
                        s_byCreature[creature] = list = new List<ConfigEntry<int>>();
                    }
                    list.Add(entry);
                }
                AlwaysEitrPlugin.Log.LogInfo(
                    $"Summoning staff: {staffName} ({itemPrefab.name}) summons {string.Join(", ", creatures)}; normal limit {NormalLimit(summon)}.");
            }
        }

        /// <summary>The vanilla limit in words: the cast limit, lowered by the skill level-ups or the staff's quality.</summary>
        private static string NormalLimit(SpawnAbility spawn)
        {
            int cap = spawn.m_maxSpawned;
            if (spawn.m_setMaxInstancesFromWeaponLevel)
            {
                return cap > 0 ? $"one per upgrade level of the staff, at most {cap}" : "one per upgrade level of the staff";
            }
            var steps = new List<string>();
            if (spawn.m_levelUpSettings != null)
            {
                var settings = new List<SpawnAbility.LevelUpSettings>(spawn.m_levelUpSettings);
                settings.Sort((a, b) => a.m_skillLevel.CompareTo(b.m_skillLevel));
                foreach (SpawnAbility.LevelUpSettings setting in settings)
                {
                    if (setting.m_maxSpawns > 0)
                    {
                        int n = cap > 0 ? Mathf.Min(setting.m_maxSpawns, cap) : setting.m_maxSpawns;
                        steps.Add(setting.m_skillLevel > 0 ? $"{n} from skill level {setting.m_skillLevel}" : n.ToString());
                    }
                }
            }
            if (steps.Count > 0)
            {
                return string.Join(", ", steps);
            }
            return cap > 0 ? cap.ToString() : "none";
        }

        private static string Localize(string token)
        {
            return Localization.instance != null ? Localization.instance.Localize(token) : token;
        }

        /// <summary>BepInEx setting names can't contain these.</summary>
        private static string SafeKey(string name)
        {
            foreach (char c in "=\n\t\\\"'[]")
            {
                name = name.Replace(c.ToString(), "");
            }
            return name.Trim();
        }

        /// <summary>The attack's projectile, or what that projectile spawns on hit.</summary>
        private static void AddSpawnAbilities(Attack attack, List<SpawnAbility> into)
        {
            GameObject projectile = attack != null ? attack.m_attackProjectile : null;
            if (projectile == null)
            {
                return;
            }
            SpawnAbility direct = projectile.GetComponent<SpawnAbility>();
            if (direct != null)
            {
                into.Add(direct);
            }
            Projectile proj = projectile.GetComponent<Projectile>();
            SpawnAbility onHit = proj != null && proj.m_spawnOnHit != null ? proj.m_spawnOnHit.GetComponent<SpawnAbility>() : null;
            if (onHit != null)
            {
                into.Add(onHit);
            }
        }

        internal static int MaxForStaff(ItemDrop.ItemData item)
        {
            if (!AlwaysEitrPlugin.Enabled.Value || item == null || item.m_dropPrefab == null)
            {
                return 0;
            }
            return s_byStaff.TryGetValue(item.m_dropPrefab.name, out ConfigEntry<int> entry) ? entry.Value : 0;
        }

        internal static int MaxForCreature(GameObject creature)
        {
            if (!AlwaysEitrPlugin.Enabled.Value
                || !s_byCreature.TryGetValue(Utils.GetPrefabName(creature), out List<ConfigEntry<int>> entries))
            {
                return 0;
            }
            int max = 0; // several staffs summoning one creature: the highest setting wins
            foreach (ConfigEntry<int> entry in entries)
            {
                max = Mathf.Max(max, entry.Value);
            }
            return max;
        }
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
    internal static class ObjectDB_Awake_Patch
    {
        private static void Postfix(ObjectDB __instance) => Summons.Scan(__instance);
    }

    [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
    internal static class ObjectDB_CopyOtherDB_Patch
    {
        private static void Postfix(ObjectDB __instance) => Summons.Scan(__instance);
    }

    /// <summary>Limit 1. Setup runs on this cast's own SpawnAbility instance, so the prefab is never changed.</summary>
    [HarmonyPatch(typeof(SpawnAbility), nameof(SpawnAbility.Setup))]
    internal static class SpawnAbility_Setup_Patch
    {
        private static void Prefix(SpawnAbility __instance, Character owner, ItemDrop.ItemData item)
        {
            if (owner == null || owner != Player.m_localPlayer)
            {
                return;
            }
            int max = Summons.MaxForStaff(item);
            if (max > 0)
            {
                __instance.m_maxSpawned = max;
            }
        }
    }

    /// <summary>Limit 2, for summons following the local player.</summary>
    [HarmonyPatch(typeof(Tameable), nameof(Tameable.UnsummonMaxInstances))]
    internal static class Tameable_UnsummonMaxInstances_Patch
    {
        private static void Prefix(Tameable __instance, ref int maxInstances)
        {
            Player local = Player.m_localPlayer;
            if (local == null || __instance.m_monsterAI == null || __instance.m_monsterAI.GetFollowTarget() != local.gameObject)
            {
                return;
            }
            int max = Summons.MaxForCreature(__instance.gameObject);
            if (max > 0)
            {
                maxInstances = max;
            }
        }
    }
}
