using System.Collections.Generic;
using System.Linq;
using CashRegister;
using RimWorld;
using Storefront.Store;
using Verse;
using Verse.AI;

namespace Storefront.Selling
{
	public class WorkGiver_StandBy : WorkGiver_Scanner
	{
		public override PathEndMode PathEndMode => PathEndMode.Touch;

		public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.GetAllStores().SelectMany(r => r.Registers).Distinct().ToArray();
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			if (!(t is Building_CashRegister register)) return false;
			if (!register.HasToWork(pawn) || !register.standby) return false;
			//if (StoreUtility.IsRegionDangerous(pawn, JobUtility.MaxDangerServing, register.GetRegion()) && !forced) return false;
			return true;
		}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			var register = (Building_CashRegister) t;

			return JobMaker.MakeJob(SellingDefOf.Storefront_StandBy, register, register.InteractionCell);
		}
	}
}
