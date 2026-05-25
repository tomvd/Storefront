using System.Linq;
using CashRegister;
using RimWorld;
using Storefront.Shopping;
using Storefront.Store;
using Storefront.Utilities;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using JobDriver_BuyItem = Storefront.Shopping.JobDriver_BuyItem;
using JobUtility = Storefront.Shopping.JobUtility;

namespace Storefront.Selling
{
    public static class Toils_Selling
    {
        public static Toil WaitForBetterJob(TargetIndex registerInd)
        {
            // Talk to patron
            var toil = new Toil();

            toil.initAction = InitAction;
            toil.tickAction = TickAction;
            toil.socialMode = RandomSocialMode.Normal;
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.FailOnDestroyedOrNull(registerInd);

            return toil;

            void InitAction()
            {
                toil.actor.pather.StopDead();
                if(toil.actor.CurJob?.GetTarget(registerInd).Thing is Building_CashRegister register)
                {
                    var offset = register.InteractionCell - register.Position;
                    toil.actor.rotationTracker.FaceCell(toil.actor.Position + offset);
                }
            }

            void TickAction()
            {
                if (toil.actor.CurJob?.GetTarget(registerInd).Thing is Building_CashRegister register)
                {
                    if (!register.HasToWork(toil.actor) || !register.standby)
                    {
                        toil.actor.jobs.curDriver.ReadyForNextToil();
                        return;
                    }
                }
                else
                {
                    Log.Message($"Waiting - register disappeared.");
                    toil.actor.jobs.curDriver.ReadyForNextToil();
                    return;
                }

                toil.actor.GainComfortFromCellIfPossible(1);

                if (toil.actor.IsHashIntervalTick(35))
                {
                    toil.actor.jobs.CheckForJobOverride();
                }

                if (toil.actor.IsHashIntervalTick(113))
                {
                    if (toil.actor.Position.GetThingList(toil.actor.Map).OfType<Pawn>().Any(p => p != toil.actor))
                    {
                        toil.actor.jobs.curDriver.ReadyForNextToil();
                    }
                }
            }
        }

        public static Toil AnnounceSelling(TargetIndex customerInd, TargetIndex itemInd)
        {
            var toil = Toils_Interpersonal.Interact(customerInd, InteractionDefOf.Chitchat);
            toil.defaultDuration = 200;
            toil.socialMode = RandomSocialMode.Off;
            toil.activeSkill = () => SkillDefOf.Social;
            toil.tickAction = TickAction;
            toil.initAction = InitAction;
            return toil;

            void InitAction()
            {
                Pawn actor = toil.actor;
                Job curJob = actor.CurJob;
                LocalTargetInfo targetCustomer = curJob.GetTarget(customerInd);
                LocalTargetInfo targetItem = curJob.GetTarget(itemInd);

                var customer = targetCustomer.Pawn;
                if (!targetCustomer.HasThing || customer == null)
                {
                    Log.Warning($"Can't announce selling. No customer.");
                    return;
                }

                if (HospitalityCompat.GetMoney(customer) < 1) {
                    Log.Warning($"Can't announce selling. Customer is broke.");
                    return;
                }

                var buyJob = customer.jobs.curDriver as JobDriver_BuyItem;

                if (buyJob == null)
                {
                    Log.Warning($"Can't announce selling. Customer is not buying anymore.");
                    return;
                }

                if (!buyJob.WaitingInQueue)
                {
                    Log.Warning($"Can't announce selling. Customer is not waiting in queue.");
                    return;
                }

                var item = targetItem.Thing;
                if (!targetItem.HasThing || item == null)
                {
                    Log.Warning($"Can't announce selling. No item.");
                    return;
                }

                buyJob.WaitingInQueue = false; // this makes the buyer stop waiting in queue
                JobUtility.GiveServiceThought(customer, toil.actor);

                bool urgent = customer.needs?.food?.CurCategory >= HungerCategory.UrgentlyHungry;
                if (urgent) toil.defaultDuration = 50;
            }

            void TickAction()
            {
                toil.actor.rotationTracker.FaceCell(toil.actor.CurJob.GetTarget(customerInd).Cell);
            }
        }

        public static Toil serveCustomer(TargetIndex customerInd, TargetIndex itemInd, TargetIndex registerInd)
        {
            var toil = new Toil {atomicWithPrevious = true};
            toil.initAction = InitAction;
            return toil;

            void InitAction()
            {
                Pawn actor = toil.actor;
                var curJob = actor.CurJob;
                var targetCustomer = curJob.GetTarget(customerInd);
                var customer = targetCustomer.Pawn;
                var targetItem = curJob.GetTarget(itemInd);
                var item = targetItem.Thing;
                var targetRegister = curJob.GetTarget(registerInd);
                var register = targetRegister.Thing;
                SellThing(actor, customer, item, register as Building_CashRegister);
                MoteMaker.MakeInteractionBubble(actor, customer, ThingDefOf.Mote_Speech, InteractionDefOf.BuildRapport.GetSymbol());
                actor.skills.GetSkill(SkillDefOf.Social).Learn(150);
                if (customer.jobs.curDriver is JobDriver_BuyItem buyJob)
                    buyJob.WaitingToBeServed = false; // this makes the buyer go on with his business - job is done
            }
        }

        private static void SellThing(Pawn salesperson, Pawn customer, Thing item, Building_CashRegister register)
        {
            if (item.MarketValue <= 0) return;

            var inventory = customer.inventory.innerContainer;

            // Note: vanilla caravan visitors carry no personal silver.
            // silver may be null here — that is fine; any shortfall is conjured below.
            Thing silver = inventory.FirstOrDefault(i => i.def == ThingDefOf.Silver);

            var itemCost = StorefrontUtility.GetPurchasingCost(item, customer, salesperson);
            int count = customer.jobs.curJob.count;
            var price = Mathf.CeilToInt(count * itemCost);

            var map = customer.MapHeld;
            var inventoryItemsBefore = inventory.ToArray();
            var thing = item.SplitOff(count);

            // Notification
            var text = price <= 0 ? "GuestTookFreeItem" : "GuestBoughtItem";
            Messages.Message(text.Translate(new NamedArgument(customer.Faction, "FACTION"), price, new NamedArgument(customer, "PAWN"), new NamedArgument(thing.GetInnerIfMinified(), "ITEM")), customer, MessageTypeDefOf.SilentInput);

            int tookItems;
            if (thing.def.IsApparel && thing is Apparel apparel
                && ApparelUtility.HasPartsToWear(customer, apparel.def)
                && HospitalityCompat.AlienFrameworkAllowsIt(customer.def, apparel.def, "CanWear"))
            {
                customer.apparel.Wear(apparel);
                tookItems = apparel.stackCount;
            }
            else if (thing.def.IsWeapon && thing is ThingWithComps equipment
                     && equipment.def.IsWithinCategory(ThingCategoryDefOf.Weapons)
                     && HospitalityCompat.AlienFrameworkAllowsIt(customer.def, thing.def, "CanEquip"))
            {
                var primary = customer.equipment.Primary;
                if (equipment.def.equipmentType == EquipmentType.Primary && primary != null)
                    if (!customer.equipment.TryTransferEquipmentToContainer(primary, customer.inventory.innerContainer))
                        Log.Message(customer.Name.ToStringShort + " failed to take " + primary + " to his inventory.");

                customer.equipment.AddEquipment(equipment);
                customer.equipment.Notify_EquipmentAdded(equipment);
                equipment.def.soundInteract?.PlayOneShot(new TargetInfo(customer.Position, customer.Map));
                tookItems = equipment.stackCount;
            }
            else
            {
                tookItems = inventory.TryAdd(thing, count);
                // could not pick up item? make it disappear
                if (tookItems == 0)
                {
                    tookItems = thing.stackCount;
                    thing.Destroy();
                }
            }

            if (tookItems > 0)
            {
                // Transfer silver to register — works for ALL visitors, not just Hospitality guests.
                // Vanilla caravan visitors carry no personal silver; any shortfall is conjured so
                // the store always receives the full price (represents payment from faction wealth).
                if (price > 0)
                {
                    if (register is not null)
                    {
                        int transferred = 0;
                        if (silver != null)
                        {
                            transferred = customer.inventory.innerContainer.TryTransferToContainer(
                                silver, register.GetDirectlyHeldThings(), price);
                        }
                        // Conjure whatever is still owed (covers visitors with no/partial silver).
                        if (transferred < price)
                        {
                            int debt = price - transferred;
                            var conjured = ThingMaker.MakeThing(ThingDefOf.Silver);
                            conjured.stackCount = debt;
                            register.GetDirectlyHeldThings().TryAdd(conjured, true);
                        }
                        // Income tracked here — inside the null guard so GetLinkedStore never receives null.
                        map.GetStoresManager().GetLinkedStore(register).AddToIncomeToday(price);
                    }
                    else
                    {
                        // Register is gone — drop silver on the ground; income is forfeited.
                        Log.Message($"Register is gone — dropping silver on the ground.");
                        if (silver != null)
                        {
                            customer.inventory.innerContainer.TryDrop(silver, customer.Position, map,
                                ThingPlaceMode.Near, price, out _);
                        }
                        else
                        {
                            // No register and no inventory silver — conjure and drop on the ground.
                            var conjured = ThingMaker.MakeThing(ThingDefOf.Silver);
                            conjured.stackCount = price;
                            GenPlace.TryPlaceThing(conjured, customer.Position, map, ThingPlaceMode.Near);
                        }
                    }
                }

                // Record what was purchased and fire vanilla trade hooks.
                // Track 'thing' directly so apparel (worn) and weapons (equipped) are covered —
                // they go to worn/equipment slots and never appear in innerContainer.
                if (!thing.Destroyed)
                {
                    if (HospitalityCompat.IsLoaded)
                        HospitalityBridge.TrackBoughtItem(customer, thing.thingIDNumber);
                    if (thing is ThingWithComps soldTwc)
                        soldTwc.PreTraded(TradeAction.PlayerSells, salesperson, customer);
                }
                // Also sweep innerContainer for any additional items (e.g., a stack that TryAdd
                // merged into an existing slot produces a new thingID we haven't handled yet).
                foreach (var currentItem in customer.inventory.innerContainer.Except(inventoryItemsBefore))
                {
                    if (!thing.Destroyed && currentItem.thingIDNumber == thing.thingIDNumber) continue;
                    if (HospitalityCompat.IsLoaded)
                        HospitalityBridge.TrackBoughtItem(customer, currentItem.thingIDNumber);
                    if (currentItem is ThingWithComps subTwc)
                        subTwc.PreTraded(TradeAction.PlayerSells, salesperson, customer);
                }

                JoyUtility.TryGainRecRoomThought(customer);
            }
            else
            {
                // Failed to equip or fit in inventory — drop it
                if (!GenDrop.TryDropSpawn(thing, customer.Position, map, ThingPlaceMode.Near, out _))
                    Log.Warning(customer.Name.ToStringShort + " failed to buy and failed to drop " + thing.Label);
            }
        }
    }
}
