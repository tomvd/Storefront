using RimWorld;
using Storefront.Store;
using UnityEngine;
using Verse;

namespace Storefront.Selling
{
	public class Alert_NoCashier : Alert
	{
		protected string explanationKey;
		private float nextCheck;
		private bool getReport;

		// ReSharper disable once PublicConstructorInAbstractClass
		public Alert_NoCashier()
		{
			defaultLabel = "AlertNoCashier".Translate();
			defaultExplanation = "AlertNoCashierExplanation".Translate();
			defaultPriority = AlertPriority.High;
		}
		public override string GetLabel() => defaultLabel;

		public override AlertReport GetReport()
		{
			if (Time.realtimeSinceStartup > nextCheck)
			{
				nextCheck = Time.realtimeSinceStartup + 1.5f;
				CheckMaps();
			}

			return getReport;
		}

		private void CheckMaps()
		{
			getReport = false;

            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || !map.mapPawns.AnyColonistSpawned) continue;
                foreach (var restaurant in map.GetComponent<StoresManager>().Stores)
                {
                    if (restaurant == null) continue;
                    if (restaurant.Register.shifts.Any(s => s.assigned.Count > 0)) continue;

                    getReport = true;
                    break;
                }
            }
        }
	}
}
