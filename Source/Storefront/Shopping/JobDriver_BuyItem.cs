using System.Collections.Generic;
using Hospitality;
using RimWorld;
using Storefront.Utilities;
using Verse;
using Verse.AI;

namespace Storefront.Shopping
{
   
    public class JobDriver_BuyItem : JobDriver
    {
        public bool WaitingInQueue = false;
        public bool WaitingToBeServed= false;
        //Constants
        public const int MinShoppingDuration = 50;
        public const int MaxShoppingDuration = 100;

        //Properties
      
        private TargetIndex ItemInd = TargetIndex.A;
        private TargetIndex RegisterInd = TargetIndex.B;
        private TargetIndex QueueInd = TargetIndex.C;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA.Thing, job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            //this.FailOnDespawnedNullOrForbidden(ItemInd);
            
            //Log.Message($"{pawn.NameShortColored} is buying {TargetThingA.LabelShort} .");

            //this.FailOn(() => !ItemUtility.IsBuyableNow(pawn, TargetThingA));
            //AddEndCondition(() =>
            //{
            //    if (Deliveree.health.ShouldGetTreatment)
            //        return JobCondition.Ongoing;
            //    return JobCondition.Succeeded;
            //});

            if (TargetThingA != null)
            {
                //yield return Toils_Reserve.Reserve(ItemInd);
                yield return Toils_Goto.GotoThing(ItemInd, PathEndMode.ClosestTouch);//.FailOnDespawnedNullOrForbidden(ItemInd);
                int duration = Rand.Range(MinShoppingDuration, MaxShoppingDuration);
                yield return Toils_General.Wait(duration).FailOnCannotTouch(ItemInd, PathEndMode.Touch);
                yield return Toils_Haul.StartCarryThing(ItemInd);
                yield return FindQueuePositionAtRegister(RegisterInd, QueueInd);
                yield return Toils_Goto.GotoCell(QueueInd, PathEndMode.OnCell);
                yield return Toils_Interpersonal.WaitToBeAbleToInteract(pawn);
                yield return Toils_General.Wait(10);
                yield return WaitInQueue(RegisterInd, QueueInd); // waits until he starts being served
                yield return WaitBeingServed(RegisterInd, QueueInd); // waits until he finishes being served
                
                //Toil toil = ToilMaker.MakeToil("BuyThing");
                //toil.initAction = () => BuyThing(toil);
                //yield return toil.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            }

            //yield return Toils_Jump.Jump(gotoToil); // shop some more
        }

        public static Toil FindQueuePositionAtRegister(TargetIndex adjacentToInd, TargetIndex cellInd, int maxRadius = 4)
        {
            Toil findCell = new Toil();
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
                    // find opposite cell
                    IntVec3 vector = target.Thing.Position - target.Thing.InteractionCell;
                    IntVec3 oppositeCell = target.Thing.Position + vector;
                    //Log.Message($"{target.Thing.Position} - {target.Thing.InteractionCell} - {oppositeCell}");
                    // Try radius 2-4
                    for (int radius = 0; radius <= maxRadius; radius++)
                    {
                        bool Validator(IntVec3 c) => c.Standable(actor.Map) && c.GetFirstPawn(actor.Map) == null;
                        if (CellFinder.TryRandomClosewalkCellNear(oppositeCell, actor.Map, radius, out var result,Validator))
                        //if (CellFinder.TryFindRandomReachableCellNear(target.Cell, actor.Map, radius, TraverseParms.For(TraverseMode.NoPassClosedDoors), Validator, null, out var result))
                        {
                            curJob.SetTarget(cellInd, result);
                            //Log.Message($"{actor.NameShortColored} found a place to stand at {result}. radius = {radius}");
                            return;
                        }
                    }

                    // This can happen if there's no space or it's crowded
                    Log.Error(actor + " could not find standable cell adjacent to " + target);
                    actor.jobs.curDriver.EndJobWith(JobCondition.Incompletable);
                }
            };
            return findCell;
        }



        public Toil WaitInQueue(TargetIndex registerInd, TargetIndex queueInd)
        {
            var toil = new Toil();
            toil.initAction = () =>
            {
                //Log.Message($"{pawn.NameShortColored} is WaitInQueue .");
                if (pawn?.jobs?.curDriver is JobDriver_BuyItem buyJob)
                {
//                    Log.Message($"{pawn.NameShortColored} is WaitInQueue GO .");
                    buyJob.WaitingInQueue = true;
                }
            };
            toil.tickAction = () => {
                if (!pawn.IsWaitingInQueue())
                {
                    //Log.Message($"{pawn.NameShortColored} is WaitInQueue DONE .");
                    pawn?.jobs?.curDriver.ReadyForNextToil();
                }
            };
            toil.AddFinishAction(() =>
            {
                //Log.Error("WaitInQueue finish toil.actor.jobs.curDriver.ticksLeftThisToil=" + toil.actor.jobs.curDriver.ticksLeftThisToil);
                /*if (pawn.IsWaitingInQueue())
                {
                    Log.Error("failed at WaitInQueue");
                }*/
            });

            toil.defaultDuration = 3000;
            //toil.WithProgressBarToilDelayReversed(queueInd, 3000, true);
            toil.WithProgressBar(queueInd, () => (float) ((double) toil.actor.jobs.curDriver.ticksLeftThisToil / (double) 3000), true, -0.5f);
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.FailOnDestroyedOrNull(registerInd);
            toil.FailOnDurationExpired(); // Duration over? Fail job!
            //toil.FailOnMyStoreClosed(); this is broken, it causes false positives TODO fix it
            toil.FailOnDangerous(Danger.None);
            return toil;
        }
        
        public Toil WaitBeingServed(TargetIndex registerInd, TargetIndex queueInd)
        {
            var toil = new Toil();
            toil.initAction = () =>
            {
                //Log.Message($"{pawn.NameShortColored} is WaitBeingServed .");
                if (pawn?.jobs?.curDriver is JobDriver_BuyItem buyJob)
                {
                    //Log.Message($"{pawn.NameShortColored} is WaitBeingServed GO.");
                    buyJob.WaitingToBeServed = true;
                }
            };
            toil.tickAction = () => {
                if (!pawn.IsWaitingToBeServed())
                {
                    JoyUtility.JoyTickCheckEnd(pawn, JoyTickFullJoyAction.None);
                    //Log.Message($"{pawn.NameShortColored} is WaitBeingServed DONE.");
                    pawn?.jobs?.curDriver.ReadyForNextToil();
                }
            };
            toil.AddFinishAction(() =>
            {
                //Log.Error("WaitBeingServed finish");
               /* if (pawn.IsWaitingToBeServed())
                {
                    Log.Error("failed at WaitBeingServed");
                }*/
            });

            toil.defaultDuration = 3000;
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.FailOnDestroyedOrNull(registerInd);
            toil.FailOnDurationExpired(); // Duration over? Fail job!
            //toil.FailOnMyStoreClosed(); this is broken, it causes false positives TODO fix it
            toil.FailOnDangerous(Danger.None);
            return toil;
        }
        
    }
}
