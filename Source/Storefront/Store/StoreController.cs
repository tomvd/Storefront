using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CashRegister;
using CashRegister.TableTops;
using RimWorld;
using Verse;

namespace Storefront.Store
{
    public class StoreController : IExposable
    {
        private readonly List<Pawn> spawnedShoppingPawnsResult = new List<Pawn>();
        private readonly List<Pawn> spawnedActiveStaffResult = new List<Pawn>();
        public IReadOnlyList<Building_CashRegister> Registers => registers;

		public Map Map { get; }
		private StoreStock stock;

        private List<Building_CashRegister> registers = new List<Building_CashRegister>();

		private int day;

		public bool IsOpenedRightNow => openForBusiness && AnyRegisterOpen;

		private bool AnyRegisterOpen => Registers.Any(r => r?.IsActive == true);

		public bool openForBusiness = true;

		public bool allowGuests = true;
		public bool allowColonists = true;
		public bool allowPrisoners = false;
		public bool allowSlaves = false;

		public float guestPricePercentage = 1;
        private string name;

        public event Action onNextDay;

		public ReadOnlyCollection<Pawn> Patrons => SpawnedShoppingPawns.AsReadOnly();
		public StoreStock Stock => stock;
		public List<Pawn> SpawnedShoppingPawns
		{
			get
			{
				spawnedShoppingPawnsResult.Clear();
                //spawnedShoppingPawnsResult.AddRange(Map.mapPawns.AllPawnsSpawned.Where(pawn => pawn.jobs?.curDriver is JobDriver_Shop && pawn.GetStoresManager().GetStoreShopping(pawn) == this));
				return spawnedShoppingPawnsResult;
			}
		}
		public List<Pawn> ActiveStaff
		{
			get
			{
				spawnedActiveStaffResult.Clear();
				var activeShifts = Registers.SelectMany(r => r.shifts.Where(s => s.IsActive));
                spawnedActiveStaffResult.AddRange(activeShifts.SelectMany(s => s.assigned).Where(p => p.MapHeld == Map));

                return spawnedActiveStaffResult;
			}
		}

        public string Name
        {
            get => name;
            set => name = value;
        }

        public bool GetIsInRange(IntVec3 position)
        {
            return Registers.Any(r => r.GetIsInRange(position));
        }

        public StoreController(Map map)
        {
            Map = map;
        }

		public void ExposeData()
		{
			Scribe_Values.Look(ref openForBusiness, "openForBusiness", true);
			Scribe_Values.Look(ref allowGuests, "allowGuests", true);
			Scribe_Values.Look(ref allowColonists, "allowColonists", true);
			Scribe_Values.Look(ref allowPrisoners, "allowPrisoners", false);
			Scribe_Values.Look(ref allowSlaves, "allowSlaves", false);
			Scribe_Values.Look(ref guestPricePercentage, "guestPricePercentage", 1);
			Scribe_Values.Look(ref day, "day");
			Scribe_Values.Look(ref name, "name");
			Scribe_Deep.Look(ref stock, "stock", this);
			Scribe_Collections.Look(ref registers, "registers", LookMode.Reference);
			InitDeepFieldsInitial();
		}

		private void InitDeepFieldsInitial()
		{
			stock ??= new StoreStock(this);
		}

		public void MapGenerated()
		{
			InitDeepFieldsInitial();
		}

		public void FinalizeInit()
		{
			InitDeepFieldsInitial();
			stock.RareTick();

			TableTop_Events.onAnyBuildingSpawned.AddListener(UpdateRegisterWithBuilding);
			TableTop_Events.onAnyBuildingDespawned.AddListener(UpdateRegisterWithBuilding);

            foreach (var register in Registers)
            {
                register.onRadiusChanged.AddListener(OnRegisterRadiusChanged);
            }
        }

        private void UpdateRegisterWithBuilding(Building building, Map map)
        {
            if (map != Map) return;
            if (building is Building_CashRegister register)
            {
                if (register.Spawned) AddRegister(register);
                else RemoveRegister(register);
            }
		}

        private void OnRegisterRadiusChanged(Building_CashRegister register)
        {
            //RefreshRegisters(null, register.Map);
			Stock.RefreshStock();
        }

        public void OnTick()
		{
			// Don't tick everything at once
			if ((GenTicks.TicksGame + Map.uniqueID) % 500 == 0) stock.RareTick();
			//if ((GenTicks.TicksGame + Map.uniqueID) % 500 == 250) orders.RareTick();
			//Log.Message($"Stock: {stock.Select(s => s.def.label).ToCommaList(true)}");
			if ((GenTicks.TicksGame + Map.uniqueID) % 500 == 300) RareTick();
		}

		private void RareTick()
		{
			if (GenDate.DaysPassed > day && GenLocalDate.HourInteger(Map) == 0) OnNextDay(GenDate.DaysPassed);
		}

		private void OnNextDay(int today)
		{
			day = today;
			onNextDay?.Invoke();
		}

		public bool MayShopHere(Pawn pawn)
		{
            if (!allowColonists && pawn.IsColonist) return false;
			if (!allowGuests && pawn.IsGuest()) return false;
			if (!allowPrisoners && pawn.IsPrisoner) return false;
			if (!allowSlaves && pawn.IsSlave) return false;
			
			return true;
		}

		public bool HasToWork(Pawn pawn)
		{
			if (!openForBusiness) return false;
			return Registers.Any(r => r?.HasToWork(pawn) == true);
		}


        public void CleanUpForRemoval()
        {
            openForBusiness = false;
        }

        public void LinkRegister(Building_CashRegister register)
        {
            foreach (var store in register.GetAllStores())
            {
                store.RemoveRegister(register);
            }

            AddRegister(register);
        }

        private void AddRegister(Building_CashRegister register)
        {
            if (!registers.Contains(register))
            {
                registers.Add(register);
                //OnRegistersChanged();
                register.onRadiusChanged.AddListener(OnRegisterRadiusChanged);
            }
        }

        public void RemoveRegister(Building_CashRegister register)
        {
            if (registers.Remove(register))
            {
                //OnRegistersChanged();
				register.onRadiusChanged.RemoveListener(OnRegisterRadiusChanged);
            }
		}

        public bool CanShopHere(Pawn pawn)
        {
            return IsOpenedRightNow && MayShopHere(pawn);
        }

    }
}
