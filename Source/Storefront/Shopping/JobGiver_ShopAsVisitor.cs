using RimWorld;
using Verse;
using Verse.AI;

namespace Storefront.Shopping
{
    /// <summary>
    /// Think node that gives vanilla caravan visitors a chance to shop at an open store.
    /// Injected at the Humanlike_PostMentalState hook, which fires before the visitor's
    /// lord-duty wander job, so we can redirect them toward the store.
    ///
    /// IMPORTANT: The vanilla JoyGiver system (JobGiver_GetJoy → JoyGiver_BuyStuff) is only
    /// evaluated along the colonist branch of the think tree; vanilla caravan visitors never
    /// reach it, regardless of whether they have a joy need.  Therefore this ThinkNode is
    /// the only shopping path available to vanilla visitors and must handle ALL of them.
    ///
    /// When Hospitality is also loaded its guests are managed by Hospitality's own duty +
    /// JoyGiver pipeline, so they are explicitly skipped here to avoid double-shopping.
    ///
    /// Non-visitor pawns (colonists, enemies, animals) fail the IsGuest check instantly.
    ///
    /// To keep shopping balanced, only a fraction of visitors will be shoppers: each pawn
    /// rolls once on arrival (seeded by pawn ID) and, if selected, uses the MTB gate below
    /// to decide when to shop.
    /// </summary>
    public class JobGiver_ShopAsVisitor : ThinkNode_JobGiver
    {
        public override Job TryGiveJob(Pawn pawn)
        {
            // Must be a friendly visitor on the player's home map
            if (!HospitalityCompat.IsGuest(pawn)) return null;

            // When Hospitality is active, its guests have a dedicated duty + JoyGiver pipeline.
            // Skip them here so they don't shop twice.
            if (HospitalityCompat.IsLoaded && HospitalityBridge.IsArrivedGuest(pawn)) return null;

            // Roughly 1-in-3 visitors are shoppers. Seeded by pawn ID so the same pawn always
            // makes the same choice (stable across think-tree calls for the same visit).
            if (Rand.ValueSeeded(pawn.thingIDNumber * 31337) > 0.33f) return null;

            // Must have money (virtual budget for vanilla visitors; see HospitalityCompat.GetMoney)
            if (HospitalityCompat.GetMoney(pawn) < 1) return null;

            // Probabilistic gate: shopper considers buying roughly once per 2 in-game hours.
            if (!Rand.MTBEventOccurs(2f, GenDate.TicksPerHour, 150)) return null;

            // Delegate to the JoyGiver worker: handles store selection, item scoring,
            // inventory-space checks, browse-vs-buy decisions, etc.
            var worker = ShoppingDefOf.Storefront_BuyStuff?.Worker;
            return worker?.TryGiveJob(pawn);
        }
    }
}
