using System.Collections.Generic;
using System.Linq;
using CashRegister;
using RimWorld;
using Storefront.Store;
using Verse;
using Verse.AI;
using StoreUtility = Storefront.Store.StoreUtility;

namespace Storefront.Selling
{
	public class WorkGiver_StandBy : WorkGiver_Scanner
	{
		public override PathEndMode PathEndMode => PathEndMode.Touch;

		public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.GetAllStores().Select(r => r.Register).Where(r => r.shifts.Select(shift => shift.assigned.Contains(pawn)).Any()).Distinct().ToArray();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			if (!(t is Building_CashRegister register)) return false;
			if (!register.HasToWork(pawn) || !register.standby) return false;
			// only one pawn working per register
			if (register.GetStore().WorkingPawns.FindAll(standbyPawn => !standbyPawn.Equals(pawn)).Any()) return false;
			if (StoreUtility.IsRegionDangerous(pawn, Danger.Some, register.GetRegion()) && !forced) return false;
			if (!register.GetStore().IsOpenedRightNow) return false;
			return true;
		}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			var register = (Building_CashRegister) t;
			if (register == null)
			{
				Log.Message("WorkGiver_StandBy register is null?");
				return null;
			}
			return JobMaker.MakeJob(SellingDefOf.Storefront_StandBy, register, register.InteractionCell);
		}
	}
}
