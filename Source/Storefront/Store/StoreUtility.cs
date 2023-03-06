using System;
using System.Collections.Generic;
using System.Linq;
using CashRegister;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Storefront.Store
{
    public static class StoreUtility
    {
        private static readonly Dictionary<Pair<Pawn, Region>, bool> dangerousRegionsCache = new Dictionary<Pair<Pawn, Region>, bool>();
        private static int lastTick;

        public static void OnTick()
        {
            if(GenTicks.TicksGame > lastTick)
                if (GenTicks.TicksGame % GenTicks.TickRareInterval == 0)
                {
                    // RARE TICK
                    dangerousRegionsCache.Clear();
                    lastTick = GenTicks.TicksGame;
                }
        }
/*
        public static bool HasShoppingQueued(this Pawn patron)
        {
            if (patron?.CurJobDef == ShoppingDefOf.Gastronomy_Dine) return true;
            return patron?.jobs.jobQueue?.Any(j => j.job.def == ShoppingDefOf.Gastronomy_Dine) == true;
        }
*/
        public static StoresManager GetStoresManager(this Thing thing)
        {
            return GetStoresManager(thing.Map);
        }

        public static StoresManager GetStoresManager(this Map map)
        {
            return map?.GetComponent<StoresManager>();
        }

        public static List<StoreController> GetAllStores(this Thing thing)
        {
            return GetStoresManager(thing.Map).stores;
        }

        public static StoreController GetStoreShopping(this Pawn patron)
        {
            return GetStoresManager(patron.Map).GetStoreShopping(patron);
        }

        public static IEnumerable<StoreController> GetAllStoresEmployed(this Pawn pawn)
        {
            return GetStoresManager(pawn.Map).stores.Where(r => r.HasToWork(pawn));
        }

        public static StoresManager GetStoresManager(this StoreController store)
        {
            return GetStoresManager(store.Map);
        }

        public static StoreController GetStore(this Building_CashRegister register)
        {
            return register.GetStoresManager().GetLinkedStore(register);
        }

        public static void GetRequestGroup(Thing thing)
        {
            foreach (ThingRequestGroup group in Enum.GetValues(typeof(ThingRequestGroup)))
            {
                if (@group == ThingRequestGroup.Undefined) continue;
                if (thing.Map.listerThings.ThingsInGroup(@group).Contains(thing))
                    Log.Message($"ShoppingSpot group: {@group}");
            }
        }

        public static bool IsRegionDangerous(Pawn pawn, Danger maxDanger, Region region = null)
        {
            region ??= pawn.GetRegion();
            var key = new Pair<Pawn, Region>(pawn, region);
            if (dangerousRegionsCache.TryGetValue(key, out bool result)) return result;

            var isRegionDangerous = region.DangerFor(pawn) > maxDanger;
            dangerousRegionsCache.Add(key, isRegionDangerous);

            return isRegionDangerous;
        }

        public static bool IsGuest(this Pawn pawn)
        {
            var faction = pawn.GetLord()?.faction;
            if (pawn.IsPrisoner) return false;
            //Log.Message($"{pawn.NameShortColored}: Faction = {faction?.GetCallLabel()} Is player = {faction?.IsPlayer} Hostile = {faction?.HostileTo(Faction.OfPlayer)}");
            return faction is {IsPlayer: false} && !faction.HostileTo(Faction.OfPlayer);
            //var isGuest = AccessTools.Method("Hospitality.GuestUtility:IsGuest");
            //Log.Message($"isGuest == null? {isGuest == null}");
            //if(isGuest != null)
            //{
            //    return (bool) isGuest.Invoke(null, new object[] {pawn, false});
            //}
            //return false;
        }

        public static int GetSilver(this Pawn pawn)
        {
            if (pawn?.inventory?.innerContainer == null) return 0;
            return pawn.inventory.innerContainer.Where(s => s.def == ThingDefOf.Silver).Sum(s => s.stackCount);
        }

        public static float GetPrice(this ThingDef mealDef, StoreController store)
        {
            if (mealDef == null) return 0;
            return mealDef.BaseMarketValue * 0.6f * store.guestPricePercentage * (1 - Find.Storyteller.difficulty.tradePriceFactorLoss);
        }

        public static T FailOnMyStoreClosedForShopping<T>(this T f) where T : IJobEndable
        {
            JobCondition OnStoreClosed()
            {
                var patron = f.GetActor();
                var store = patron.GetStoresManager().GetStoreShopping(patron);
                return store?.IsOpenedRightNow == true
                    ? JobCondition.Ongoing
                    : JobCondition.Incompletable;
            }

            f.AddEndCondition(OnStoreClosed);
            return f;
        }

        public static T FailOnHasShift<T>(this T f) where T : IJobEndable
        {
            JobCondition HasShift()
            {
                var pawn = f.GetActor();
                return pawn.GetAllStoresEmployed().Any(r=>r.ActiveStaff.Contains(pawn)) ? JobCondition.Incompletable : JobCondition.Ongoing;
            }

            f.AddEndCondition(HasShift);
            return f;
        }

        public static T FailOnNotShopping<T>(this T f, TargetIndex patronInd) where T : IJobEndable
        {
            JobCondition PatronIsNotShopping()
            {
                var patron = f.GetActor().jobs.curJob.GetTarget(patronInd).Thing as Pawn;
                //if (patron?.jobs.curDriver is JobDriver_Dine) return JobCondition.Ongoing;
                Log.Message($"Checked {patron?.NameShortColored}. Not dining >> failing {f.GetActor().NameShortColored}'s job {f.GetActor().CurJobDef?.label}.");
                return JobCondition.Incompletable;
            }

            f.AddEndCondition(PatronIsNotShopping);
            return f;
        }
/*
        public static T FailOnNotShoppingQueued<T>(this T f, TargetIndex patronInd) where T : IJobEndable
        {
            JobCondition PatronHasNoShoppingInQueue()
            {
                var patron = f.GetActor().jobs.curJob.GetTarget(patronInd).Thing as Pawn;
                if (patron.HasShoppingQueued()) return JobCondition.Ongoing;
                Log.Message($"Checked {patron?.NameShortColored}. Not planning to dine >> failing {f.GetActor().NameShortColored}'s job {f.GetActor().CurJobDef?.label}.");
                return JobCondition.Incompletable;
            }

            f.AddEndCondition(PatronHasNoShoppingInQueue);
            return f;
        }
*/
        /// <summary>
        /// Find a valid order at any store
        /// </summary>
        
        /*public static Order FindValidOrder(this Pawn patron)
        {
            if(patron == null)
            {
                Log.Warning("Patron not set.");
                return null;
            }

            return patron.GetAllStores().Select(r => r.Orders.GetOrderFor(patron)).FirstOrDefault(o => o != null); }
*/
       /* public static IEnumerable<StoreController> GetStoresServing(this ShoppingSpot diningSpot)
        {
            return diningSpot.GetStoresManager().stores.Where(r => r.diningSpots.Contains(diningSpot));
        }

        public static Order WaiterGetOrderFor(Pawn waiter, Pawn patron)
        {
            return waiter.GetAllStoresEmployed().Select(r => r.Orders.GetOrderFor(patron)).FirstOrDefault(o => o != null);
        }*/
    }
}
