using Hospitality;
using Hospitality.Utilities;
using Verse;

namespace Storefront;

/// <summary>
/// Thin wrapper around Hospitality types. All methods in this class directly reference
/// Hospitality assemblies. Only call them AFTER confirming HospitalityCompat.IsLoaded is true.
///
/// Kept in its own file so the JIT compiles these methods lazily — they are never entered
/// at runtime when Hospitality is absent, so no TypeLoadException occurs.
/// </summary>
internal static class HospitalityBridge
{
    public static bool IsArrivedGuest(Pawn pawn) => pawn.IsArrivedGuest(out _);

    public static float GetRequiresFoodFactor(Pawn pawn) => GuestUtility.GetRequiresFoodFactor(pawn);

    public static int GetMoney(Pawn pawn) => ItemUtility.GetMoney(pawn);

    public static int GetInventorySpaceFor(Pawn pawn, Thing thing) => ItemUtility.GetInventorySpaceFor(pawn, thing);

    public static bool AlienFrameworkAllowsIt(ThingDef pawnDef, ThingDef thingDef, string check) =>
        ItemUtility.AlienFrameworkAllowsIt(pawnDef, thingDef, check);

    public static bool IsFood(Thing thing) => ItemUtility.IsFood(thing);

    public static bool IsIngestible(Thing thing) => ItemUtility.IsIngestible(thing);

    public static bool IsRequiredByRoyalty(Pawn pawn, ThingDef thingDef) =>
        ItemUtility.IsRequiredByRoyalty(pawn, thingDef);

    /// <summary>
    /// Records a purchased item in the guest's CompGuest.boughtItems list.
    /// CompGuest is referenced only in the method BODY (IL), never in the signature,
    /// so reflection-based attribute scanning won't trigger a TypeLoadException when
    /// Hospitality is absent.
    /// </summary>
    public static void TrackBoughtItem(Pawn customer, int itemThingId) =>
        customer.TryGetComp<CompGuest>()?.boughtItems.Add(itemThingId);
}
