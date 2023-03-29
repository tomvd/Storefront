using System.Collections.Generic;
using System.Linq;
using CashRegister;
using CashRegister.TableTops;
using Verse;

namespace Storefront.Store
{
    /*
     * StoresManagers keeps track of a collection of stores on the map
     */
    public class StoresManager : MapComponent
    {
        public List<StoreController> Stores = new();

        public StoresManager(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref Stores, "stores", LookMode.Deep, map);
            Stores ??= new List<StoreController>();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            foreach (var store in Stores) store.FinalizeInit();
            //if (stores.Count == 0) AddStore(); // AddStore also calls FinalizeInit

            // Check unclaimed registers (when adding to an existing game with registers on the map)
            foreach (var register in map.listerBuildings.AllBuildingsColonistOfClass<Building_CashRegister>())
            {
                if (Stores.Any(r => r.Register.Equals(register))) continue;
                AddStore(register);
            }
            
            // if registers are created or removed - create and remove their stores
            TableTop_Events.onAnyBuildingSpawned.AddListener(RegisterSpawned);
            TableTop_Events.onAnyBuildingDespawned.AddListener(RegisterDespawned);
            
        }
        
        private void RegisterSpawned(Building building, Map mapSpawned)
        {
            if (mapSpawned != map) return;
            if (building is Building_CashRegister register)
            {
                AddStore(register);
            }
        }
        
        private void RegisterDespawned(Building building, Map mapSpawned)
        {
            if (mapSpawned != map) return;
            if (building is Building_CashRegister register)
            {
                DeleteStore(Stores.FirstOrDefault(r => r.Register.Equals(register)));
            }
        }

        public override void MapGenerated()
        {
            base.MapGenerated();
            foreach (var store in Stores) store.MapGenerated();
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();
            StoreUtility.OnTick();
            foreach (var store in Stores) store.OnTick();
        }

        public StoreController GetLinkedStore(Building_CashRegister register)
        {
            return Stores.FirstOrDefault(controller => controller.Register.Equals(register));
        }

        
        private void AddStore(Building_CashRegister register)
        {
            Log.Message("AddStore for new register...");
            var store = new StoreController(map);
            store.LinkRegister(register);
            Log.Message("New store is now linked to register at " + store.Register.Position);

            // Find an unused name, numbering upwards
            for (int i = 0; i < 100; i++)
            {
                var name = "StoreDefaultName".Translate(Stores.Count + i);
                if (NameIsInUse(name, store)) continue;

                store.Name = name;
                break;
            }
            Log.Message("New store got a temporary name " + store.Name);
            store.FinalizeInit();
            Stores.Add(store);
            Log.Message("We now have " + Stores.Count + " stores.");
        }

        private void DeleteStore(StoreController store)
        {
            store?.CleanUpForRemoval();
            Stores.Remove(store);
        }

        public bool NameIsInUse(string name, StoreController store)
        {
            return Stores.Any(controller => controller != store && controller.Name.EqualsIgnoreCase(name));
        }
    }
}