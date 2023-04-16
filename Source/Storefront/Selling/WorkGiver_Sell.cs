using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Storefront.Shopping;
using Storefront.Store;
using Storefront.Utilities;
using Verse;
using Verse.AI;

namespace Storefront.Selling
{
    public class WorkGiver_Sell : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) => pawn.GetAllStoresEmployed().SelectMany(r=>r.SpawnedShoppingPawns).Distinct().ToList();

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Pawn customer)) return false;

            if (pawn == t) return false;
            
            // am I standby?
            //var pawnDriver = pawn.jobs?.curDriver as JobDriver_StandBy;
            //if (pawnDriver == null) return false;

            // is there a customer waiting in the queue?
            if (customer.jobs?.curDriver is not JobDriver_BuyItem || !customer.IsWaitingInQueue()) return false;
            
            if (!customer.Spawned || customer.Dead)
            {
                Log.Message($"Sales canceled. dead? {customer.Dead} unspawned? {!customer.Spawned}");
                return false;
            }
            return true;
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Pawn customer))
            {
                Log.Message("WorkGiver_Sell customer is null");
                return null;
            }
            var driver = customer.jobs?.curDriver as JobDriver_BuyItem;
            if (driver == null)            
            {
                Log.Message("JobDriver_BuyItem customer is null");
                return null;
            }
            return JobMaker.MakeJob(SellingDefOf.Storefront_Sell, customer, driver.job.targetA, driver.job.targetB);
        }
    }
}
