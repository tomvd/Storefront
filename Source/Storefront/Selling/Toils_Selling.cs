using System.Linq;
using CashRegister;
using Hospitality;
using RimWorld;
using Storefront.Shopping;
using Storefront.Store;
using Storefront.Utilities;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using JobDriver_BuyItem = Storefront.Shopping.JobDriver_BuyItem;

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
            //toil.FailOnMentalState(patronInd);

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

                toil.actor.GainComfortFromCellIfPossible();

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

                var item = targetItem.Thing;
                if (!targetItem.HasThing || item == null)
                {
                    Log.Warning($"Can't announce selling. No item.");
                    return;
                }

                // TODO similar to gastronomy customer can have good thoughts about the service quality
                /*if (patron.jobs.curDriver is JobDriver_BuyItem patronDriver)
                {
                    DiningUtility.GiveServiceThought(customer, toil.actor, patronDriver.HoursWaited);
                }*/
                if (customer.jobs.curDriver is JobDriver_BuyItem buyJob)
                {
                    buyJob.CustomerState = CustomerState.BeingServed;
                }
                bool urgent = customer.needs?.food?.CurCategory >= HungerCategory.UrgentlyHungry;
                if (urgent) toil.defaultDuration = 50;

                // TODO money icon?
                //var symbol = item.def.uiIcon;
                //TryCreateBubble(actor, customer, InteractionDefOf.BuildRapport.GetSymbol());
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
                Log.Message($"{actor.NameShortColored} is selling.");
                var curJob = actor.CurJob;
                var targetCustomer = curJob.GetTarget(customerInd);
                var customer = targetCustomer.Pawn;
                Log.Message($"{customer.NameShortColored} is customer.");
                var targetItem = curJob.GetTarget(itemInd);
                var item = targetItem.Thing;
                Log.Message($"{item.Label} is product.");                
                var targetRegister = curJob.GetTarget(registerInd);
                var register = targetRegister.Thing;
                Log.Message($"{register.Label} is register.");
                SellThing(actor, customer, item, register as Building_CashRegister);
                //var symbol = item.def.uiIcon;
                //if (symbol != null)
                    MoteMaker.MakeInteractionBubble(actor, customer, ThingDefOf.Mote_Speech, InteractionDefOf.BuildRapport.GetSymbol());
                Log.Message($"{customer.jobs.curDriver} is curjob driver.");
                actor.skills.GetSkill(SkillDefOf.Social).Learn(150);
                if (customer.jobs.curDriver is JobDriver_BuyItem buyJob)
                    buyJob.CustomerState = CustomerState.Leaving;
            }
        }
        
        private static void SellThing(Pawn salesperson, Pawn customer, Thing item, Building_CashRegister register)
        {
            Log.Message($"BuyThing MarketValue {item.MarketValue}");
            if (item.MarketValue <= 0) return;

            var inventory = customer.inventory.innerContainer;

            Thing silver = inventory.FirstOrDefault(i => i.def == ThingDefOf.Silver);
            if (silver == null) return;
//            Log.Message($"BuyThing silver.stackCount {silver.stackCount}");

            var itemCost = StorefrontUtility.GetPurchasingCost(item, customer, salesperson);
//            Log.Message($"BuyThing itemCost {itemCost}");
            int count = customer.jobs.curJob.count;

            var price = Mathf.CeilToInt(count*itemCost);
            Log.Message($"BuyThing price {price}");

            if(silver.stackCount < price) return;

            var map = customer.MapHeld;
            var inventoryItemsBefore = inventory.ToArray();
            var thing = item.SplitOff(count);

            // Notification
            //if (Settings.enableBuyNotification)
            //{
                var text = price <= 0 ? "GuestTookFreeItem" : "GuestBoughtItem";
                Messages.Message(text.Translate(new NamedArgument(customer.Faction, "FACTION"), price, new NamedArgument(customer, "PAWN"), new NamedArgument(thing, "ITEM")), customer, MessageTypeDefOf.SilentInput);
            //}

            int tookItems;
            if (thing.def.IsApparel && thing is Apparel apparel && ApparelUtility.HasPartsToWear(customer, apparel.def) && ItemUtility.AlienFrameworkAllowsIt(customer.def, apparel.def, "CanWear"))
            {
                customer.apparel.Wear(apparel);
                tookItems = apparel.stackCount;
            }
            else if (thing.def.IsWeapon && thing is ThingWithComps equipment && equipment.def.IsWithinCategory(ThingCategoryDefOf.Weapons)
                     && ItemUtility.AlienFrameworkAllowsIt(customer.def, thing.def, "CanEquip"))
            {
                var primary = customer.equipment.Primary;
                if (equipment.def.equipmentType == EquipmentType.Primary && primary != null)
                    if (!customer.equipment.TryTransferEquipmentToContainer(primary, customer.inventory.innerContainer))
                    {
                        Log.Message(customer.Name.ToStringShort + " failed to take " + primary + " to his inventory.");
                    }
                
                customer.equipment.AddEquipment(equipment);
                customer.equipment.Notify_EquipmentAdded(equipment);
                equipment.def.soundInteract?.PlayOneShot(new TargetInfo(customer.Position, customer.Map));
                tookItems = equipment.stackCount;
            }
            else
            {
                tookItems = inventory.TryAdd(thing, count);
                Log.Message($"tookItems {tookItems}");
            }

            var comp = customer.TryGetComp<CompGuest>(); //customer.CompGuest();
            if (tookItems > 0 && comp != null)
            {
                
                if (price > 0)
                {
                    if (register is not null)
                    {
                        Log.Message($"transfer silver to register");
                        customer.inventory.innerContainer.TryTransferToContainer(silver,
                            register.GetDirectlyHeldThings(), price);
                    }
                    else
                    {
                        Log.Message($"register is gone? drop silver on the ground");
                        customer.inventory.innerContainer.TryDrop(silver, customer.Position, map, ThingPlaceMode.Near,
                            price, out silver);
                    }
                    map.GetStoresManager().GetLinkedStore(register).AddToIncomeToday(price);
                }

                // Check what's new in the inventory (TryAdd creates a copy of the original object!)
                var newItems = customer.inventory.innerContainer.Except(inventoryItemsBefore).ToArray();
                foreach (var currentItem in newItems)
                {
                    comp.boughtItems.Add(currentItem.thingIDNumber);

                    // Handle trade stuff
                    if (item is ThingWithComps twc)
                    {
                        twc.PreTraded(TradeAction.PlayerSells, salesperson, customer);
                    }
                }
            }
            else
            {
                // Failed to equip or take
                if (!GenDrop.TryDropSpawn(thing, customer.Position, map, ThingPlaceMode.Near, out _))
                {
                    Log.Warning(customer.Name.ToStringShort + " failed to buy and failed to drop " + thing.Label);
                }
            }
        }
    }
}
