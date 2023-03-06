using System.Collections.Generic;
using System.Linq;
using CashRegister;
using Verse;

namespace Storefront.Store
{
    public class StoresManager : MapComponent
    {
        public List<StoreController> stores = new List<StoreController>();
        private readonly Dictionary<Pawn, StoreController> shoppingAt = new Dictionary<Pawn, StoreController>();

        public StoresManager(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref stores, "stores", LookMode.Deep, map);
            stores ??= new List<StoreController>();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            foreach (var store in stores) store.FinalizeInit();
            if (stores.Count == 0) AddStore(); // AddStore also calls FinalizeInit

            // Check unclaimed registers
            foreach (var register in map.listerBuildings.AllBuildingsColonistOfClass<Building_CashRegister>())
            {
                if (stores.Any(r => r.Registers.Contains(register))) continue;
                stores[0].LinkRegister(register);
            }
        }

        public override void MapGenerated()
        {
            base.MapGenerated();
            foreach (var store in stores) store.MapGenerated();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            StoreUtility.OnTick();
            foreach (var store in stores) store.OnTick();
        }

        public StoreController GetLinkedStore(Building_CashRegister register)
        {
            return stores.FirstOrDefault(controller => controller.Registers.Contains(register));
        }

        
        public StoreController AddStore()
        {
            var store = new StoreController(map);
            stores.Add(store);

            // Find an unused name, numbering upwards
            for (int i = 0; i < 100; i++)
            {
                var name = "StoreDefaultName".Translate(stores.Count + i);
                if (NameIsInUse(name, store)) continue;

                store.Name = name;
                break;
            }
            store.FinalizeInit();
            return store;
        }

        public void DeleteStore(StoreController store)
        {
            store?.CleanUpForRemoval();
            stores.Remove(store);
        }

        public void RegisterShoppingAt(Pawn patron, StoreController controller)
        {
            if (shoppingAt.TryGetValue(patron, out var current))
            {
                if (current == controller)
                {
                    //Log.Message($"{patron.NameShortColored} tried to register shopping at {controller.Name}, but is already registered.");
                }
                else if (controller == null)
                {
                    shoppingAt.Remove(patron);
                    //Log.Message($"{patron.NameShortColored} has unregistered from shopping at {current.Name}.");
                }
                else
                {
                    shoppingAt.Remove(patron);
                    Log.Message($"{patron.NameShortColored} has switched from shopping at {current.Name} to {controller.Name}.");
                    shoppingAt.Add(patron, controller);
                }
            }
            else
            {
                if (controller != null)
                {
                    //Log.Message($"{patron.NameShortColored} is now registered as shopping at {controller.Name}.");
                    shoppingAt.Add(patron, controller);
                }
                else
                {
                    Log.Warning($"{patron.NameShortColored} tried to unregister shopping, but wasn't registered.");
                }
            }
        }

        public StoreController GetStoreShopping(Pawn patron)
        {
            return shoppingAt.TryGetValue(patron, out var controller) ? controller : null;
        }

        public bool NameIsInUse(string name, StoreController store)
        {
            return stores.Any(controller => controller != store && controller.Name.EqualsIgnoreCase(name));
        }
    }
}