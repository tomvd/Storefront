using System.Collections.Generic;
using System.Linq;
using CashRegister;
using Verse;

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
            return GetStoresManager(thing.Map).Stores;
        }

        public static IEnumerable<StoreController> GetAllStoresEmployed(this Pawn pawn)
        {
            return GetStoresManager(pawn.Map).Stores.Where(r => r.HasToWork(pawn));
        }

        public static StoresManager GetStoresManager(this StoreController store)
        {
            return GetStoresManager(store.Map);
        }

        public static StoreController GetStore(this Building_CashRegister register)
        {
            return register.GetStoresManager().GetLinkedStore(register);
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
    }
}
