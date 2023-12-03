using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using CashRegister;
using RimWorld;
using Storefront.Selling;
using Storefront.Shopping;
using Storefront.Utilities;
using Verse;

namespace Storefront.Store
{
	/*
	 * StoreController keeps track of everything going on in a store
	 *  - settings, inventory, register, staff, customers, statistics
	 */
    public class StoreController : IExposable
    {
	    public Building_CashRegister Register => register;

		public Map Map { get; }

		// one register per store
        private Building_CashRegister register;

		private int day;

		public bool IsOpenedRightNow => openForBusiness && AnyRegisterOpen;

		private bool AnyRegisterOpen => register?.IsActive == true;

		public bool openForBusiness = false;

		public float guestPricePercentage = 1;
        private string name;
        
        public float incomeYesterday;
        public float incomeToday;
        
        private ThingFilter storeFilter;

		public ReadOnlyCollection<Pawn> Customers => SpawnedShoppingPawns.AsReadOnly();
		public List<Pawn> SpawnedShoppingPawns { get; } = new List<Pawn>();

		public List<Pawn> ActiveStaff { get; } = new List<Pawn>();

		public List<Pawn> WorkingPawns { get; } = new List<Pawn>();

		public List<Thing> Stock { get; } = new List<Thing>();

		private void UpdateCaches(bool odd)
		{
			if (odd)
			{
				var pawns = Map.mapPawns.AllPawnsSpawned;
				SpawnedShoppingPawns.Clear();
				SpawnedShoppingPawns.AddRange(pawns.Where(pawn =>
					pawn.jobs?.curDriver is JobDriver_BuyItem buyJob && buyJob.job.targetB.Thing == Register));

				WorkingPawns.Clear();
				WorkingPawns.AddRange(pawns.Where(pawn =>
					(pawn.jobs?.curDriver is JobDriver_StandBy standByJob && standByJob.job.targetA.Thing == Register)
					|| pawn.jobs?.curDriver is JobDriver_Sell sellJob && sellJob.job.targetA.Thing == Register));
			}
			else
			{
				var activeShifts = Register.shifts.Where(s => s.IsActive);
				ActiveStaff.Clear();
				ActiveStaff.AddRange(activeShifts.SelectMany(s => s.assigned).Where(p => p.MapHeld == Map));

				Stock.Clear();
				//Stopwatch sw = new Stopwatch();
				//sw.Start();
				Stock.AddRange(AllThingsInRange(Map, register)
					.Where(t => StorefrontUtility.IsBuyableAtAll(null, t, this)).Take(50).ToList());
				//Log.Message("UpdateCaches took " + sw.ElapsedMilliseconds + "ms");
				//sw.Stop();
			}
		}
        public string Name
        {
            get => name;
            set => name = value;
        }
        
        public static IEnumerable<Thing> AllThingsInRange(Map map, Building_CashRegister register)
        {
	        HashSet<Thing> yieldedThings = new HashSet<Thing>();
	        foreach (IntVec3 tradeableCell in register.Fields)
	        {
		        List<Thing> thingList = tradeableCell.GetThingList(map);
		        for (int i = 0; i < thingList.Count; i++)
		        {
			        Thing t = thingList[i];
			        if (t.def.category == ThingCategory.Item && !yieldedThings.Contains(t))
			        {
				        yieldedThings.Add(t);
				        yield return t;
			        }
		        }
	        }
        }

        public bool GetIsInRange(IntVec3 position)
        {
            return Register.GetIsInRange(position);
        }

        public StoreController(Map map)
        {
            Map = map;
        }

		public void ExposeData()
		{
			Scribe_Values.Look(ref openForBusiness, "openForBusiness", false);
			Scribe_Values.Look(ref guestPricePercentage, "guestPricePercentage", 1);
			Scribe_Values.Look(ref incomeToday, "incomeToday");
			Scribe_Values.Look(ref incomeYesterday, "incomeYesterday");
			Scribe_Values.Look(ref day, "day");
			Scribe_Values.Look(ref name, "name");
			Scribe_References.Look(ref register, "register");
			Scribe_Deep.Look(ref storeFilter, "storeFilter");		
		}
		
		private void OnNextDay()
		{
			incomeYesterday = incomeToday;
			incomeToday = 0;
			if (incomeYesterday > 0)
			{
				Messages.Message("StoreMessageIncomeToday".Translate(incomeYesterday.ToStringMoney()), MessageTypeDefOf.NeutralEvent);
			}
		}

		public void FinalizeInit()
		{
            register.onRadiusChanged.AddListener(OnRegisterRadiusChanged);
        }

        private void OnRegisterRadiusChanged(Building_CashRegister register)
        {
            UpdateCaches(false);
        }

        public void OnTick()
		{
			// Don't tick everything at once
			if ((GenTicks.TicksGame + Map.uniqueID) % 200 == 0) UpdateCaches(true);
			if ((GenTicks.TicksGame + Map.uniqueID) % 200 == 100) UpdateCaches(false);
			if ((GenTicks.TicksGame + Map.uniqueID) % 500 == 300) RareTick();
		}

		private void RareTick()
		{
			if (GenDate.DaysPassed > day && GenLocalDate.HourInteger(Map) == 0) OnNextDay(GenDate.DaysPassed);
		}

		private void OnNextDay(int today)
		{
			day = today;
			OnNextDay();
		}

		public bool HasToWork(Pawn pawn)
		{
			if (!openForBusiness) return false;
			return register?.HasToWork(pawn) == true;
		}


        public void CleanUpForRemoval()
        {
            openForBusiness = false;
        }

        public void LinkRegister(Building_CashRegister register)
        {
	        this.register = register;
	        this.register.onRadiusChanged.AddListener(OnRegisterRadiusChanged);
        }

        public bool CanShopHere(Pawn pawn)
        {
            return IsOpenedRightNow && ActiveStaff.Count > 0;
        }

        public void AddToIncomeToday(float amount)
        {
	        incomeToday += amount;
        }
        
        public ThingFilter GetStoreFilter()
        {
	        if (storeFilter == null) InitStoreFilter();
	        return storeFilter;
        }
        
        public bool IsForSale(ThingDef def)
        {
	        if (storeFilter == null) InitStoreFilter();
	        return storeFilter.Allows(def);
        }

        private void InitStoreFilter()
        {
	        storeFilter = new ThingFilter();
	        storeFilter.SetFromPreset(StorageSettingsPreset.DefaultStockpile);
        }
    }
}
