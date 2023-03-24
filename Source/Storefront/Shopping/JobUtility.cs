using RimWorld;
using Storefront.Store;
using Storefront.Utilities;
using UnityEngine;
using Verse;
using Verse.AI;
using StoreUtility = Storefront.Store.StoreUtility;

namespace Storefront.Shopping
{
    public static class JobUtility
    {
        public static T FailOnDangerous<T>(this T f, Danger maxDanger) where T : IJobEndable
        {
            JobCondition OnRegionDangerous()
            {
                Pawn pawn = f.GetActor();
                var check = StoreUtility.IsRegionDangerous(pawn, maxDanger, pawn.GetRegion());
                if (!check) return JobCondition.Ongoing;
                Log.Message($"{pawn.NameShortColored} failed {pawn.CurJobDef.label} because of danger ({pawn.GetRegion().DangerFor(pawn)})");
                return JobCondition.Incompletable;
            }

            f.AddEndCondition(OnRegionDangerous);
            return f;
        }

        public static T FailOnDurationExpired<T>(this T f) where T : IJobEndable
        {
            JobCondition OnDurationExpired()
            {
                var pawn = f.GetActor();
                if (pawn.jobs.curDriver.ticksLeftThisToil > 0) return JobCondition.Ongoing;
                StorefrontUtility.GiveWaitThought(pawn);
                Log.Message($"{pawn.NameShortColored} ended {pawn.CurJobDef?.label} because of wait timeout.");
                return JobCondition.Incompletable;
            }

            f.AddEndCondition(OnDurationExpired);
            return f;
        }
        
        public static T FailOnMyStoreClosed<T>(this T f) where T : IJobEndable
        {
            JobCondition OnRestaurantClosed()
            {
                var patron = f.GetActor();
                var myStore = patron.GetStoresManager().Stores.Find(store => store.Patrons.Contains(patron));
                return myStore?.IsOpenedRightNow == true
                    ? JobCondition.Ongoing
                    : JobCondition.Incompletable;
            }

            f.AddEndCondition(OnRestaurantClosed);
            return f;
        }
        
        public static void GiveServiceThought(Pawn customer, Pawn cashier)
        {
            if (customer.needs.mood == null) return;

            int stage = GetServiceStage(customer, cashier);
            customer.needs.mood.thoughts.memories.TryGainMemory(ThoughtMaker.MakeThought(ShoppingDefOf.Storefront_Serviced, stage), cashier);
            customer.needs.mood.thoughts.memories.TryGainMemory(ThoughtMaker.MakeThought(ShoppingDefOf.Storefront_ServicedMood, stage));
        }

        private static int GetServiceStage(Pawn customer, Pawn cashier)
        {
            float score = 1 * cashier.GetStatValue(StatDefOf.SocialImpact);
            score += cashier.story.traits.DegreeOfTrait(TraitDefOf.Industriousness) * 0.25f;
            score += cashier.story.traits.DegreeOfTrait(TraitDefOf.Beauty) * 0.25f;
            score += cashier.story.traits.HasTrait(TraitDefOf.Kind) ? 0.25f : 0;
            score += customer.story.traits.HasTrait(TraitDefOf.Kind) ? 0.15f : 0;
            score += cashier.story.traits.HasTrait(TraitDefOf.Abrasive) ? -0.2f : 0;
            score += cashier.story.traits.HasTrait(TraitDefOf.AnnoyingVoice) ? -0.2f : 0;
            score += cashier.story.traits.HasTrait(TraitDefOf.CreepyBreathing) ? -0.1f : 0;
            if(cashier.needs.mood != null) score += (cashier.needs.mood.CurLevelPercentage - 0.5f) * 0.6f; // = +-0.3
            score += customer.relations.OpinionOf(cashier) / 200f; // = +-0.5
            int stage = Mathf.RoundToInt(Mathf.Clamp(score, 0, 2)*2); // 0-4
            //Log.Message($"Service score of {waiter.NameShortColored} serving {patron.NameShortColored}:\n"
            //            + $"opinion = {patron.relations.OpinionOf(waiter) * 1f / 200:F2}, mood = {(waiter.needs.mood.CurLevelPercentage - 0.5f) * 0.6f} final = {score:F2}, stage = {stage}");

            return stage;
        }
    }
}
