using System;
using System.Collections.Generic;
using System.Linq;
using Hospitality;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using ItemUtility = Hospitality.ItemUtility;
using CashRegister;

namespace Storefront.Shopping
{
    public class JobDriver_BuyItem : JobDriver
    {
        //Constants
        public const int MinShoppingDuration = 75;
        public const int MaxShoppingDuration = 300;

        //Properties
        private Thing Item => job.GetTarget(TargetIndex.A).Thing;
        private Thing Register => job.GetTarget(TargetIndex.B).Thing;
        
        private const TargetIndex IndexSpot = TargetIndex.B;
        private const TargetIndex IndexStanding = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA.Thing, job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            
            Log.Message($"{pawn.NameShortColored} is buying {Item.LabelShort} at {IndexSpot}.");

            this.FailOn(() => !ItemUtility.IsBuyableNow(pawn, Item));
            //AddEndCondition(() =>
            //{
            //    if (Deliveree.health.ShouldGetTreatment)
            //        return JobCondition.Ongoing;
            //    return JobCondition.Succeeded;
            //});

            if (TargetThingA != null)
            {
                yield return Toils_Reserve.Reserve(TargetIndex.A);
                yield return QueueAtRegister(IndexSpot, IndexStanding); // A is first the queue spot, then where we'll stand
                yield return Toils_Goto.GotoCell(IndexStanding, PathEndMode.OnCell);
                yield return Toils_Interpersonal.WaitToBeAbleToInteract(pawn);
                //int duration = Rand.Range(MinShoppingDuration, MaxShoppingDuration);
                //yield return Toils_General.Wait(duration).FailOnCannotTouch(TargetIndex.B, PathEndMode.Touch);
                // TODO customer needs to wait until the cashier is ready to serve ...

                Toil takeThing = new Toil();
                takeThing.initAction = () => BuyThing(takeThing);
                yield return takeThing.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            }

            //yield return Toils_Jump.Jump(gotoToil); // shop some more
        }

        private void BuyThing(Toil toil)
        {
            Job curJob = toil.actor.jobs.curJob; 
            //Toils_Haul.ErrorCheckForCarry(toil.actor, Item);
            if (curJob.count == 0)
            {
                throw new Exception(string.Concat("BuyItem job had count = ", curJob.count, ". Job: ", curJob));
            }

            if (Item.MarketValue <= 0) return;
            int maxSpace = ItemUtility.GetInventorySpaceFor(toil.actor, Item);
            var inventory = toil.actor.inventory.innerContainer;

            Thing silver = inventory.FirstOrDefault(i => i.def == ThingDefOf.Silver);
            if (silver == null) return;

            var itemCost = ItemUtility.GetPurchasingCost(Item);
            var maxAffordable = itemCost <= 0 ? 3 : Mathf.FloorToInt(silver.stackCount/itemCost); // don't buy more than x of free stuff
            if (maxAffordable < 1) return;

            // Changed formula a bit, so guests are less likely to leave small stacks if they can afford it
            var maxWanted = Rand.RangeInclusive(1, maxAffordable);
            int count = Mathf.Min(Item.stackCount, maxSpace, maxWanted);

            var price = Mathf.CeilToInt(count*itemCost);

            if(silver.stackCount < price) return;

            var map = toil.actor.MapHeld;
            var inventoryItemsBefore = inventory.ToArray();
            var thing = Item.SplitOff(count);

            // Notification
            //if (Settings.enableBuyNotification)
            //{
                var text = price <= 0 ? "GuestTookFreeItem" : "GuestBoughtItem";
                Messages.Message(text.Translate(new NamedArgument(toil.actor.Faction, "FACTION"), price, new NamedArgument(toil.actor, "PAWN"), new NamedArgument(thing, "ITEM")), toil.actor, MessageTypeDefOf.SilentInput);
            //}

            int tookItems;
            if (thing.def.IsApparel && thing is Apparel apparel && ApparelUtility.HasPartsToWear(pawn, apparel.def) && ItemUtility.AlienFrameworkAllowsIt(toil.actor.def, apparel.def, "CanWear"))
            {
                toil.actor.apparel.Wear(apparel);
                tookItems = apparel.stackCount;
            }
            else if (thing.def.IsWeapon && thing is ThingWithComps equipment && equipment.def.IsWithinCategory(ThingCategoryDefOf.Weapons)
                     && ItemUtility.AlienFrameworkAllowsIt(toil.actor.def, thing.def, "CanEquip"))
            {
                var primary = pawn.equipment.Primary;
                if (equipment.def.equipmentType == EquipmentType.Primary && primary != null)
                    if (!pawn.equipment.TryTransferEquipmentToContainer(primary, pawn.inventory.innerContainer))
                    {
                        Log.Message(pawn.Name.ToStringShort + " failed to take " + primary + " to his inventory.");
                    }
                
                pawn.equipment.AddEquipment(equipment);
                pawn.equipment.Notify_EquipmentAdded(equipment);
                equipment.def.soundInteract?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                tookItems = equipment.stackCount;
            }
            else
            {
                tookItems = inventory.TryAdd(thing, count);
            }

            var comp = toil.actor.TryGetComp<CompGuest>(); //toil.actor.CompGuest();
            if (tookItems > 0 && comp != null)
            {
                if (price > 0)
                {
                    //inventory.TryDrop(silver, toil.actor.Position, map, ThingPlaceMode.Near, price, out silver);
                    if (price < silver.stackCount)
                    {
                        Thing silverToPay = silver.SplitOff(count);
                        silver.TryAbsorbStack(silverToPay, false);
                        Register.TryGetInnerInteractableThingOwner().TryAdd(silverToPay);
                    }
                    if (price == silver.stackCount)
                    {
                        Register.TryGetInnerInteractableThingOwner().TryAdd(silver);
                    }
                }

                // Check what's new in the inventory (TryAdd creates a copy of the original object!)
                var newItems = toil.actor.inventory.innerContainer.Except(inventoryItemsBefore).ToArray();
                foreach (var item in newItems)
                {
                    comp.boughtItems.Add(item.thingIDNumber);

                    // Handle trade stuff
                    Trade(toil, item, map);
                }
            }
            else
            {
                // Failed to equip or take
                if (!GenDrop.TryDropSpawn(thing, toil.actor.Position, map, ThingPlaceMode.Near, out _))
                {
                    Log.Warning(toil.actor.Name.ToStringShort + " failed to buy and failed to drop " + thing.Label);
                }
            }
        }

        private void Trade(Toil toil, Thing item, Map map)
        {
            if (item is ThingWithComps twc && map.mapPawns.FreeColonistsSpawnedCount > 0)
            {
                twc.PreTraded(TradeAction.PlayerSells, map.mapPawns.FreeColonistsSpawned.RandomElement(), toil.actor);
            }

            // Register with lord toil  - TODO - but how?
            //var lord = pawn.GetLord();
            //var lordToil = lord?.CurLordToil as LordToil_VisitPoint;

            //lordToil?.OnPlayerSoldItem(item);
        }
        
        public static Toil QueueAtRegister(TargetIndex adjacentToInd, TargetIndex cellInd, int maxRadius = 4)
        {
            Toil findCell = new Toil {atomicWithPrevious = true};
            findCell.initAction = delegate {
                Pawn actor = findCell.actor;
                Job curJob = actor.CurJob;
                LocalTargetInfo target = curJob.GetTarget(adjacentToInd);
                if (target.HasThing && (!target.Thing.Spawned || target.Thing.Map != actor.Map))
                {
                    Log.Error(actor + " could not find standable cell adjacent to " + target + " because this thing is either unspawned or spawned somewhere else.");
                    actor.jobs.curDriver.EndJobWith(JobCondition.Errored);
                }
                else
                {
                    // Try radius 2-4
                    for (int radius = 0; radius <= maxRadius; radius++)
                    {
                        bool Validator(IntVec3 c) => c.Standable(actor.Map) && c.GetFirstPawn(actor.Map) == null;
                        if (CellFinder.TryFindRandomReachableCellNear(target.Cell, actor.Map, radius, TraverseParms.For(TraverseMode.NoPassClosedDoors), Validator, null, out var result))
                        {
                            curJob.SetTarget(cellInd, result);
                            //Log.Message($"{actor.NameShortColored} found a place to stand at {result}. radius = {radius}");
                            return;
                        }
                    }

                    // This can happen if there's no space or it's crowded
                    //Log.Error(actor + " could not find standable cell adjacent to " + target);
                    actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                }
            };
            return findCell;
        }        
    }
}
