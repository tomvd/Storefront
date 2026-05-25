using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Storefront;

/// <summary>
/// Public compatibility layer between Storefront and Hospitality.
///
/// When Hospitality is loaded, methods here delegate to HospitalityBridge (which wraps
/// Hospitality's own utility classes). When it is not loaded, vanilla-compatible fallback
/// implementations are used so the mod works with caravan visitors out of the box.
/// </summary>
public static class HospitalityCompat
{
    private static bool? _isLoaded;

    /// <summary>True when the Hospitality mod is active in the current run.</summary>
    public static bool IsLoaded => _isLoaded ??= ModLister.HasActiveModWithName("Hospitality");

    // -------------------------------------------------------------------------
    // Guest detection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true when the pawn is a friendly visitor who can potentially shop:
    /// a Hospitality guest *or* a vanilla caravan visitor on the player's home map.
    /// </summary>
    public static bool IsGuest(Pawn pawn)
    {
        // Animals (pack animals in caravans) pass most checks but can't shop and would
        // crash on pawn.story/pawn.skills access further down the shopping pipeline.
        if (!pawn.RaceProps.Humanlike) return false;
        if (pawn.Faction == null) return false;
        if (pawn.Faction == Faction.OfPlayer) return false;
        if (pawn.Faction.HostileTo(Faction.OfPlayer)) return false;
        if (!pawn.Spawned || pawn.Map == null || !pawn.Map.IsPlayerHome) return false;
        // Must be part of an organised lord (visiting/trading caravan), not a lone wanderer.
        if (pawn.GetLord() == null) return false;
        return true;
    }

    // -------------------------------------------------------------------------
    // Inventory helpers
    // -------------------------------------------------------------------------

    /// <summary>How much silver the pawn is carrying (or can access).</summary>
    /// <remarks>
    /// When Hospitality is active, delegates to <see cref="HospitalityBridge.GetMoney"/> which
    /// reads <c>CompGuest.spendingMoney</c>.
    ///
    /// Without Hospitality, vanilla caravan visitors carry no personal silver — their faction's
    /// wealth is assumed to cover purchases (silver is conjured at the point of sale in
    /// <see cref="Storefront.Selling.Toils_Selling"/>). We therefore return a fixed virtual
    /// budget so that all money-gate checks pass for eligible guests. Non-guest pawns (colonists,
    /// enemies, neutrals without a lord) still return 0 so they can never shop.
    /// </remarks>
    public static int GetMoney(Pawn pawn)
    {
        if (IsLoaded) return HospitalityBridge.GetMoney(pawn);

        // Check actual inventory silver first (in case some mod or scenario gives visitors silver).
        int personal = pawn.inventory?.innerContainer?
            .Where(t => t.def == ThingDefOf.Silver)
            .Sum(t => t.stackCount) ?? 0;
        if (personal > 0) return personal;

        // Give visiting guests a virtual budget so they pass money-gate checks.
        // The real silver is generated at point of sale — see Toils_Selling.SellThing.
        return IsGuest(pawn) ? 200 : 0;
    }

    /// <summary>How many of <paramref name="thing"/> the pawn can still carry.</summary>
    public static int GetInventorySpaceFor(Pawn pawn, Thing thing)
    {
        if (IsLoaded) return HospitalityBridge.GetInventorySpaceFor(pawn, thing);
        float freeSpace = MassUtility.FreeSpace(pawn);
        float itemMass = thing.GetStatValue(StatDefOf.Mass);
        if (itemMass <= 0f) return thing.stackCount;
        return Mathf.Max(1, Mathf.FloorToInt(freeSpace / itemMass));
    }

    // -------------------------------------------------------------------------
    // Food / hunger helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns how urgently the pawn wants food (0 = full, ~1 = near-starving).
    /// Used to weight food items higher in the shopping priority.
    /// </summary>
    public static float GetRequiresFoodFactor(Pawn pawn)
    {
        if (IsLoaded) return HospitalityBridge.GetRequiresFoodFactor(pawn);
        var food = pawn.needs?.food;
        if (food == null) return 0f;
        // Mirroring Hospitality's rough behaviour: factor rises sharply below half-full.
        return Mathf.InverseLerp(0.5f, 0.05f, food.CurLevelPercentage);
    }

    /// <summary>True when the thing provides nutrition.</summary>
    public static bool IsFood(Thing thing)
    {
        if (IsLoaded) return HospitalityBridge.IsFood(thing);
        return thing.def.IsNutritionGivingIngestible;
    }

    /// <summary>True when the thing can be ingested at all.</summary>
    public static bool IsIngestible(Thing thing)
    {
        if (IsLoaded) return HospitalityBridge.IsIngestible(thing);
        return thing.def.ingestible != null;
    }

    // -------------------------------------------------------------------------
    // Mod-compat helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Checks Humanoid Alien Races compatibility for wearing/equipping/eating an item.
    /// Returns true when AlienFramework is not installed or has no objection.
    /// </summary>
    public static bool AlienFrameworkAllowsIt(ThingDef pawnDef, ThingDef thingDef, string check)
    {
        if (IsLoaded) return HospitalityBridge.AlienFrameworkAllowsIt(pawnDef, thingDef, check);
        return true;
    }

    /// <summary>True when the item is required by a royalty title the pawn holds.</summary>
    public static bool IsRequiredByRoyalty(Pawn pawn, ThingDef thingDef)
    {
        if (IsLoaded) return HospitalityBridge.IsRequiredByRoyalty(pawn, thingDef);
        return false;
    }

    // -------------------------------------------------------------------------
    // Map-level helpers (used by WorkGiver_StandBy setting)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true when at least one potential shopping customer is currently on
    /// <paramref name="map"/> — either a Hospitality guest or a vanilla caravan visitor.
    /// </summary>
    public static bool AnyCustomersOnMap(Map map)
    {
        return map.mapPawns.AllPawnsSpawned.Any(IsGuest);
    }
}
