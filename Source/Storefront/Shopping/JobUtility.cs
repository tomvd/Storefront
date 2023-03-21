using RimWorld;
using Storefront.Store;
using Storefront.Utilities;
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
    }
}
