using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Storefront.Store;
using Storefront.Utilities;
using UnityEngine;
using Verse;
using Verse.AI;
using FoodUtility = RimWorld.FoodUtility;

namespace Storefront.Shopping
{
    public class JoyGiver_BuyStuff : JoyGiver
    {
        public JoyGiverDefShopping Def => (JoyGiverDefShopping) def;
        private readonly Dictionary<int, List<ulong>> recentlyLookedAt = new Dictionary<int, List<ulong>>(); // Pawn ID, list of cell hashes
        protected virtual int OptimalMoneyForShopping => 30;


        public override void GetSearchSet(Pawn pawn, List<Thing> outCandidates)
        {
            outCandidates.Clear();
            outCandidates.AddRange(pawn.Map.listerThings.ThingsInGroup(Def.requestGroup));
        }

        public override float GetChance(Pawn pawn)
        {
            if (!HospitalityCompat.IsGuest(pawn)) return 0;
            if (pawn.skills.GetSkill(SkillDefOf.Social).TotallyDisabled) return 0;
            var money = HospitalityCompat.GetMoney(pawn);
            if (money == 0) return 0;
            return base.GetChance(pawn);
        }

        // This TryGiveJob can either give a BrowseItem job - if the pawn is not interested in buying the thing, but just looking at it
        //    or a BuyItem job - if the pawn is interested enough in buying it.
        public override Job TryGiveJob(Pawn pawn)
        {
            if (HospitalityCompat.GetMoney(pawn) < 1) return null;

            var requiresFoodFactor = HospitalityCompat.GetRequiresFoodFactor(pawn);
            var map = pawn.MapHeld;

            // pawn.needs.food can be null for pawns without a food need (e.g. some special races).
            if (pawn.needs?.food != null)
                requiresFoodFactor += (int)pawn.needs.food.CurCategory;

            // Try 5 random items from a pool of all items of all stores
            List<Thing> things = new List<Thing>();
            pawn.GetAllStores().Where(r => r.CanShopHere(pawn)).ToList().ForEach(store => things.AddRange(store.Stock
                .Where(t => !HasRecentlyLookedAt(pawn, t.Position) && StorefrontUtility.IsBuyableAtAll(pawn, t, store))));

            if (things.Count == 0) return null;

            List<Thing> selection = things.Where(t =>
                pawn.CanReach(t.Position, PathEndMode.Touch, Danger.Some, false, false, TraverseMode.PassDoors)
                && HospitalityCompat.GetInventorySpaceFor(pawn, t) > 0).ToList().TakeRandom(10).Distinct().ToList();

            if (selection.Count == 0) return null;

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

            bool urgent = pawn.needs?.food?.CurCategory >= HungerCategory.UrgentlyHungry && HospitalityCompat.IsFood(thing);
            Pawn seller = store.ActiveStaff.MaxBy(pawn => pawn.skills.GetSkill(SkillDefOf.Social).Level);
            if (seller == null) return null; // staff left between the CanShopHere check and here

            // the higher the social skill of the staff, the more chance the customer will buy (every skill point counts as 0.01 interesting factor)
            interestingFactor += seller.skills.GetSkill(SkillDefOf.Social).Level / 100.0f;

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
            int maxSpace = HospitalityCompat.GetInventorySpaceFor(pawn, thing);
            var maxCanBuy = thing.stackCount;
            if (thing.stackCount > 1)
            {
                int money = HospitalityCompat.GetMoney(pawn);
                var itemCost = StorefrontUtility.GetPurchasingCost(thing, pawn, seller);
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
            Job buyJob = new Job(ShoppingDefOf.Storefront_BuyItem, thing, store.Register);
            buyJob.count = count;
            return buyJob;
        }

        private static float Likey(Pawn pawn, Thing thing, float requiresFoodFactor)
        {
            if (thing == null) return 0;
            thing = thing.GetInnerIfMinified();

            // Health of object
            var hpFactor = thing.def.useHitPoints?(float)thing.HitPoints/thing.MaxHitPoints:1;

            // Apparel
            var appFactor = thing is Apparel apparel ? 1 + ApparelScoreGain(pawn, apparel) : 0.8f; // Not apparel, less likey

            if (thing.def.IsArt)
            {
                appFactor = 2;
            }

            // Food
            if(HospitalityCompat.IsFood(thing) && pawn.RaceProps.CanEverEat(thing))
            {
                appFactor = FoodUtility.FoodOptimality(pawn, thing, FoodUtility.GetFinalIngestibleDef(thing), 0, true) / 300f; // 300 = optimality max
                appFactor += requiresFoodFactor;
                if (thing.def.IsWithinCategory(ThingCategoryDefOf.PlantFoodRaw)) appFactor -= 0.25f;
                if (thing.def.IsWithinCategory(ThingCategoryDefOf.MeatRaw)) appFactor -= 0.5f;
            }
            // Other consumables
            else if (HospitalityCompat.IsIngestible(thing) && thing.def.ingestible.joy > 0)
            {
                appFactor = 1 + thing.def.ingestible.joy*0.5f;

                // Hungry? Care less about other stuff
                if(requiresFoodFactor > 0) appFactor -= requiresFoodFactor / 3;

                if (thing.def.IsNonMedicalDrug)
                {
                    if (pawn.IsTeetotaler()) return 0;
                    if (pawn.story.traits.HasTrait(TraitDefOf.DrugDesire)) return 1;
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
                appFactor = 1;
                if (pawn.RaceProps.Humanlike && pawn.WorkTagIsDisabled(WorkTags.Violent)) return 0;
                if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return 0;
                if (!HospitalityCompat.AlienFrameworkAllowsIt(pawn.def, thing.def, "CanEquip")) return 0;
            }
            // Shield belt
            if (thing.def.IsShieldThatBlocksRanged)
            {
                if (pawn.equipment.Primary?.def.IsRangedWeapon == true) return 0;
                if (!HospitalityCompat.AlienFrameworkAllowsIt(pawn.def, thing.def, "CanEquip")) return 0;
            }

            // Quality of object
            var qFactor = 0.7f;
            if (thing.TryGetQuality(out var cat))
            {
                qFactor = (float) cat;
                qFactor -= (float) QualityCategory.Normal;
                qFactor /= (float) QualityCategory.Masterwork - (float) QualityCategory.Normal;
                qFactor += 1;
            }
            // Tech level of object
            var tFactor = 0.8f;
            if (thing.def.techLevel != TechLevel.Undefined)
            {
                tFactor = (float) thing.def.techLevel;
                tFactor -= (float) pawn.Faction.def.techLevel;
                tFactor /= (float) TechLevel.Spacer;
                tFactor += 1;
            }
            var rFactor = Rand.RangeSeeded(0.9f, 1.2f, pawn.thingIDNumber*60509 + thing.thingIDNumber*33151);
            return Mathf.Max(0, hpFactor*hpFactor*qFactor*qFactor*tFactor*appFactor*rFactor);
        }

        // Copied so we can make some adjustments
        public static float ApparelScoreGain(Pawn pawn, Apparel ap)
        {
            if (ap.def.IsShieldThatBlocksRanged && pawn.equipment.Primary?.def.IsWeaponUsingProjectiles == true)
                return -1000;
            if (!HospitalityCompat.AlienFrameworkAllowsIt(pawn.def, ap.def, "CanWear"))
                return -1000;
            if (!ApparelUtility.HasPartsToWear(pawn, ap.def))
                return -1000;
            if (pawn.story.traits.HasTrait(TraitDefOf.Nudist)) return -1000;
            float num = RimWorld.JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, ap);
            List<Apparel> wornApparel = pawn.apparel.WornApparel;
            bool flag = false;
            var newReq = HospitalityCompat.IsRequiredByRoyalty(pawn, ap.def);

            for (int i = 0; i < wornApparel.Count; ++i)
            {
                if (!ApparelUtility.CanWearTogether(wornApparel[i].def, ap.def, pawn.RaceProps.body))
                {
                    if (pawn.apparel.IsLocked(wornApparel[i])) return -1000;
                    var wornReq = HospitalityCompat.IsRequiredByRoyalty(pawn, wornApparel[i].def);
                    if (wornReq && !newReq) return -1000;
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
            return thing.def.IsNutritionGivingIngestible
                   && thing.def.IsWithinCategory(ThingCategoryDefOf.Foods)
                   && HospitalityCompat.AlienFrameworkAllowsIt(pawn.def, thing.def, "CanEat");
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
