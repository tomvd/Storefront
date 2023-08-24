using System.Collections.Generic;
using System.Linq;
using Hospitality;
using Hospitality.Utilities;
using RimWorld;
using Storefront.Store;
using Storefront.Utilities;
using UnityEngine;
using Verse;
using Verse.AI;
using FoodUtility = RimWorld.FoodUtility;
using GuestUtility = Hospitality.Utilities.GuestUtility;
using ItemUtility = Hospitality.ItemUtility;

namespace Storefront.Shopping
{
    public class JoyGiver_BuyStuff : JoyGiver
    {
        public JoyGiverDefShopping Def => (JoyGiverDefShopping) def;
        private readonly Dictionary<int, List<ulong>> recentlyLookedAt = new Dictionary<int, List<ulong>>(); // Pawn ID, list of cell hashes
        protected virtual int OptimalMoneyForShopping => 30;


        protected override void GetSearchSet(Pawn pawn, List<Thing> outCandidates)
        {
            outCandidates.Clear();
            outCandidates.AddRange(pawn.Map.listerThings.ThingsInGroup(Def.requestGroup));
        }

        public override float GetChance(Pawn pawn)
        {
            if (!pawn.IsArrivedGuest(out _)) return 0;
            if (pawn.skills.GetSkill(SkillDefOf.Social).TotallyDisabled) return 0;
            //if (!pawn.MayBuy()) return 0;
            //if (pawn.GetShoppingArea() == null) return 0;
            var money = pawn.GetMoney();
            //Log.Message(pawn.NameShortColored + " has " + money + " silver left.");
            return Mathf.InverseLerp(1, OptimalMoneyForShopping, money)*base.GetChance(pawn);
        }

        // This TryGiveJob can either give a BrowseItem job - if the pawn is not interested in buying the thing, but just looking at it
        //    or a BuyItem job - if the pawn is interested enough in buying it.
        public override Job TryGiveJob(Pawn pawn)
        {
            //Log.Message($"{pawn.NameShortColored} is going to shop as joygiver.");

            //pawn.GetStoresManager().RegisterShoppinAt(pawn, store);
            if (pawn.GetMoney() < 1) return null;

            var requiresFoodFactor = GuestUtility.GetRequiresFoodFactor(pawn);
            var map = pawn.MapHeld;

            //Log.Message("requiresFoodFactor {pawn.NameShortColored}:" + requiresFoodFactor);
            requiresFoodFactor += (int)pawn.needs.food.CurCategory;
            //Log.Message("requiresFoodFactor taking into account his need: {pawn.NameShortColored}:" + requiresFoodFactor);

            // Try 5 random items from a pool of all items of all stores
            List<Thing> things = new List<Thing>();
            pawn.GetAllStores().Where(r => r.CanShopHere(pawn)).ToList().ForEach(store => things.AddRange(store.Stock
                .Where(t => !HasRecentlyLookedAt(pawn, t.Position) && StorefrontUtility.IsBuyableAtAll(pawn, t, store))));

            if (things.Count == 0)
            {
                //Log.Message($"failed to provide any qualifying item for {pawn.NameShortColored} .");
                return null;
            }
            
            /*foreach (var thing1 in things)
            {
                Log.Message($"things for {pawn.NameShortColored}:" + thing1.LabelShort);
            }*/


            List<Thing> selection = things.Where(t =>
                pawn.CanReach(t.Position, PathEndMode.Touch, Danger.Some, false, false, TraverseMode.PassDoors)
                && ItemUtility.GetInventorySpaceFor(pawn, t) > 0).ToList().TakeRandom(10).Distinct().ToList();
            /*foreach (var thing1 in selection)
            {
                Log.Message($"selection for {pawn.NameShortColored}:" + thing1.LabelShort + " Likey = " + Likey(pawn, thing1, requiresFoodFactor));
            }*/
            
            if (selection.Count == 0)
            {
                //Log.Message($"failed to provide any qualifying selection of items for {pawn.NameShortColored} .");
                return null;
            }            

            Thing thing = selection.MaxBy(t => Likey(pawn, t, requiresFoodFactor));

            if (thing == null)
            {
                Log.Message("huh?");
                return null;
            }
            var interestingFactor = Likey(pawn, thing, requiresFoodFactor);
            // find the store selling the item
            StoreController store = pawn.GetAllStores().FirstOrDefault(store => store.GetIsInRange(thing.Position));
            if (store == null)
            {
                Log.Message($"Store is gone?");
                return null; // huh?
            }
            //Log.Message($"{pawn.NameShortColored} wants to shop at store ({store.Name}).");

            bool urgent = pawn.needs?.food?.CurCategory >= HungerCategory.UrgentlyHungry && thing.IsFood();
            Pawn seller = store.ActiveStaff.MaxBy(pawn => pawn.skills.GetSkill(SkillDefOf.Social).Level);

            // the higher the social skill of the staff, the more chance the customer will buy (every skill point counts as 0.01 interesting factor)
            interestingFactor += seller.skills.GetSkill(SkillDefOf.Social).Level / 100.0f;

            //Log.Message(thing.Label + ": " + interestingFactor + " interesting for " + pawn.NameShortColored);

            if (!urgent && (interestingFactor <= 0.5f || Rand.Chance(0.3f)))
            {
                int duration = Rand.Range(JobDriver_BuyItem.MinShoppingDuration, JobDriver_BuyItem.MaxShoppingDuration);
                var canBrowse = CellFinder.TryRandomClosewalkCellNear(thing.Position, map, 2, out var standTarget);
                if (canBrowse)
                {
                    RegisterLookedAt(pawn, thing.Position);
                    return new Job(ShoppingDefOf.Storefront_BrowseItems, standTarget, thing) {expiryInterval = duration * 4};
                }
                Log.Error($"{pawn.NameShortColored} cannot access ({thing.Label}).");
                return null;
            }

            // calculate count here already instead of with buying... this means we have to have enough worst case money, since we dont know the price(seller TPI) yet
            int maxSpace = ItemUtility.GetInventorySpaceFor(pawn, thing);
            //Log.Message($"BuyThing maxSpace {maxSpace}");       
            var maxCanBuy = thing.stackCount;
            if (thing.stackCount > 1)
            {
                int money = ItemUtility.GetMoney(pawn);
                var itemCost = StorefrontUtility.GetPurchasingCost(thing, pawn, seller);
                //Log.Message($"TryGiveJob itemCost {itemCost}");
                var maxAffordable = Mathf.FloorToInt(money / itemCost);
                if (maxAffordable < 1)
                {
                    maxAffordable = 1;
                }
                maxCanBuy = Rand.RangeInclusive(1, Mathf.Min(thing.stackCount, maxSpace, maxAffordable));
            }

            var count = maxCanBuy;
            if (count == 0)
            {
                Log.Error($"{pawn.NameShortColored} cannot buy ({thing.Label}).");
                return null;                
            }
            //Log.Message($"{pawn.NameShortColored} is going to take {thing.LabelShort}x{count} and queue at {store.Register.LabelShort}.");
            Job buyJob = new Job(ShoppingDefOf.Storefront_BuyItem, thing, store.Register);
            buyJob.count = count;
            return buyJob;
        }

        private static float Likey(Pawn pawn, Thing thing, float requiresFoodFactor)
        {
            if (thing == null) return 0;
            //Log.Message($"{pawn.LabelShort} checking out {thing.LabelShort}.");

            // Health of object
            var hpFactor = thing.def.useHitPoints?(float)thing.HitPoints/thing.MaxHitPoints:1;
            //Log.Message(thing.Label + " - health: " + hpFactor);
            
            // Apparel
            var appFactor = thing is Apparel apparel ? 1 + ApparelScoreGain(pawn, apparel) : 0.8f; // Not apparel, less likey
            //Log.Message(thing.Label + " - apparel score: " + appFactor);

            // Food
            if(ItemUtility.IsFood(thing) && pawn.RaceProps.CanEverEat(thing))
            {
                appFactor = FoodUtility.FoodOptimality(pawn, thing, FoodUtility.GetFinalIngestibleDef(thing), 0, true) / 300f; // 300 = optimality max
//                Log.Message($"{pawn.LabelShort} added {requiresFoodFactor} to the score for his hunger and {appFactor} for food optimality.");
                appFactor += requiresFoodFactor;
                if (thing.def.IsWithinCategory(ThingCategoryDefOf.PlantFoodRaw)) appFactor -= 0.25f;
                if (thing.def.IsWithinCategory(ThingCategoryDefOf.MeatRaw)) appFactor -= 0.5f;
            }
            // Other consumables
            else if (ItemUtility.IsIngestible(thing) && thing.def.ingestible.joy > 0)
            {
                appFactor = 1 + thing.def.ingestible.joy*0.5f;

                // Hungry? Care less about other stuff
                if(requiresFoodFactor > 0) appFactor -= requiresFoodFactor / 3;
               
                if (thing.def.IsNonMedicalDrug)
                {
                    if (pawn.IsTeetotaler())
                    {
  //                      Log.Message("Teetotalers dont buy non-medical drugs");
                        return 0;
                    }
                    
                    if (pawn.story.traits.HasTrait(TraitDefOf.DrugDesire))
                    {
    //                    Log.Message("Addict wants his drugs");
                        return 1;
                    }
                }
            }
            else
            {
                // Hungry? Care less about other stuff
                if(requiresFoodFactor > 0) appFactor -= requiresFoodFactor / 3;
            }

            if (CompBiocodable.IsBiocoded(thing) && !CompBiocodable.IsBiocodedFor(thing, pawn)) return 0;

            // Weapon
            if (thing.def.IsRangedWeapon)
            {
                if (pawn.story.traits.HasTrait(TraitDefOf.Brawler)) return 0;
                if (pawn.apparel.WornApparel.Exists(apparelObject => apparelObject.def.IsShieldThatBlocksRanged)) return 0;
            }

            if (thing.def.IsWeapon)
            {
                // Weapon is also good!
                appFactor = 1;
                if (pawn.RaceProps.Humanlike && pawn.WorkTagIsDisabled(WorkTags.Violent)) return 0;
                if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return 0;
                if (!ItemUtility.AlienFrameworkAllowsIt(pawn.def, thing.def, "CanEquip")) return 0;
            }
            // Shield belt
            if (thing.def.IsShieldThatBlocksRanged)
            {
                if (pawn.equipment.Primary?.def.IsRangedWeapon == true) return 0;
                if (!ItemUtility.AlienFrameworkAllowsIt(pawn.def, thing.def, "CanEquip")) return 0;
            }

            // Quality of object
            var qFactor = 0.7f;
            if (thing.TryGetQuality(out var cat))
            {
                qFactor = (float) cat;
                qFactor -= (float) QualityCategory.Normal;
                qFactor /= (float) QualityCategory.Masterwork - (float) QualityCategory.Normal;
                qFactor += 1;
                //Log.Message(thing.Label+" - quality: "+cat+" = "+ qFactor);
            }
            // Tech level of object
            var tFactor = 0.8f;
            if (thing.def.techLevel != TechLevel.Undefined)
            {
                tFactor = (float) thing.def.techLevel;
                tFactor -= (float) pawn.Faction.def.techLevel;
                tFactor /= (float) TechLevel.Spacer;
                tFactor += 1;
                //Log.Message(thing.Label + " - techlevel: " + thing.def.techLevel + " = " + tFactor);
            }
            var rFactor = Rand.RangeSeeded(0.9f, 1.2f, pawn.thingIDNumber*60509 + thing.thingIDNumber*33151);
            //if(hpFactor*hpFactor*qFactor*qFactor*tFactor*appFactor > 0.5) 
            //Log.Message($"{thing.LabelShort.Colorize(Color.yellow)} - score: {hpFactor * hpFactor * qFactor * qFactor * tFactor * appFactor}, random: {rFactor}");
            return Mathf.Max(0, hpFactor*hpFactor*qFactor*qFactor*tFactor*appFactor*rFactor);
        }

        // Copied so we can make some adjustments
        public static float ApparelScoreGain(Pawn pawn, Apparel ap)
        {
            if (ap.def.IsShieldThatBlocksRanged && pawn.equipment.Primary?.def.IsWeaponUsingProjectiles == true)
                return -1000;
            // Added
            if (!ItemUtility.AlienFrameworkAllowsIt(pawn.def, ap.def, "CanWear")) 
                return -1000;
            if (!ApparelUtility.HasPartsToWear(pawn, ap.def))
                return -1000;
            if (pawn.story.traits.HasTrait(TraitDefOf.Nudist)) return -1000;
            //if (PawnApparelGenerator.IsHeadgear(ap.def)) return 0;
            float num = RimWorld.JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, ap);
            List<Apparel> wornApparel = pawn.apparel.WornApparel;
            bool flag = false;
            // Added:
            var newReq = ItemUtility.IsRequiredByRoyalty(pawn, ap.def);

            for (int i = 0; i < wornApparel.Count; ++i)
            {
                if (!ApparelUtility.CanWearTogether(wornApparel[i].def, ap.def, pawn.RaceProps.body))
                {
                    if (pawn.apparel.IsLocked(wornApparel[i])) return -1000;
                    // Added: 
                    var wornReq = ItemUtility.IsRequiredByRoyalty(pawn, wornApparel[i].def);
                    if (wornReq && !newReq) return -1000;
                    //if (!pawn.outfits.forcedHandler.AllowedToAutomaticallyDrop(wornApparel[index]))
                    //    return -1000f;
                    num -= JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, wornApparel[i]);
                    flag = true;
                }
            }
            if (!flag)
                num *= 10f;
            return num;
        }

        protected virtual bool Qualifies(Thing thing, Pawn pawn)
        {
            return Def.requestGroup.Includes(thing.def);
        }

        public static bool CanEat(Thing thing, Pawn pawn)
        {
            return thing.def.IsNutritionGivingIngestible && thing.def.IsWithinCategory(ThingCategoryDefOf.Foods) && ItemUtility.AlienFrameworkAllowsIt(pawn.def, thing.def, "CanEat");
        }

        private bool HasRecentlyLookedAt(Pawn pawn, IntVec3 cell)
        {
            return recentlyLookedAt.TryGetValue(pawn.thingIDNumber, out var hashes) && hashes.Contains(cell.UniqueHashCode());
        }

        private void RegisterLookedAt(Pawn pawn, IntVec3 cell)
        {
            if (recentlyLookedAt.TryGetValue(pawn.thingIDNumber, out var hashes))
            {
                hashes.Add(cell.UniqueHashCode());
                const int MaxCellsToRemember = 5;
                if (hashes.Count > MaxCellsToRemember) hashes.RemoveAt(0);
            }
            else recentlyLookedAt.Add(pawn.thingIDNumber, new List<ulong> {cell.UniqueHashCode()});
        }
    }
}
