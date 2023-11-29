using System;
using Hospitality;
using JetBrains.Annotations;
using RimWorld;
using Storefront.Shopping;
using Storefront.Store;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Storefront.Utilities;

public static class StorefrontUtility
{
    public static float maxTPI = 0.6423f;
    public static float basePriceModifier = 0.72f;
    public static float maxPriceModifier = 0.90f;
    
    public static bool IsBuyableAtAll(Pawn pawn, Thing thing, StoreController store)
    {
        if (!store.GetIsInRange(thing.Position)) return false;
        if (thing.def.Minifiable)
        {
            if (!(thing is MinifiedThing))
            {
                //Log.Message(thing.Label+": Minifiable not MinifiedThing ");
                return false;
            }
        }
        thing = thing.GetInnerIfMinified();
        
        if (!store.IsForSale(thing.def))
        {
            //Log.Message(thing.Label+": IsForSale ");
            return false;
        }        

        if (thing.def.isUnfinishedThing)
        {
            Log.Message(thing.Label+": isUnfinishedThing ");
            return false;
        }

        if (!TradeUtility.EverPlayerSellable(thing.def))
        {
            Log.Message(thing.Label+": EverPlayerSellable ");
            return false;
        } 

        if (thing.def == ThingDefOf.Silver) return false;

        if (!thing.def.tradeability.PlayerCanSell())
        {
            Log.Message(thing.Label+": PlayerCanSell ");
            return false;
        }
            
        
        //if (!pawn.MayPurchaseThing(thing)) return false;

        if (thing.def.thingSetMakerTags != null && thing.def.thingSetMakerTags.Contains("NotForGuests"))
        {
            Log.Message(thing.Label+": thingSetMakerTags ");
            return false;
        }

        if (!thing.SpawnedOrAnyParentSpawned)
        {
            Log.Message(thing.Label+": SpawnedOrAnyParentSpawned ");
            return false;
        }

        if (thing.ParentHolder is Pawn)
        {
            Log.Message(thing.Label+": ParentHolder ");
            return false;
        }

        if (thing.IsForbidden(Faction.OfPlayer))
        {
            Log.Message(thing.Label+": IsForbidden ");
            return false;
        }

        if (pawn == null) return true; // shortcut here, we are not checking for a specific pawn
        
        // Put all pawn checks below here

        if (!pawn.HasReserved(thing) && !pawn.CanReserve(thing))
        {
            return false;
        }
        
        //if (!thing.IsSociallyProper(pawn))
        //{
        //    Log.Message(thing.Label + ": is not proper for " + pawn.NameStringShort);
        //    return false;
        //}
        // we actually might want to buy something above our budget if the skill of the seller is high enough
        float skill = store.ActiveStaff.MaxBy(p => p.skills.GetSkill(SkillDefOf.Social).Level).skills
            .GetSkill(SkillDefOf.Social).Level;
        
        //Log.Message("sell skill " + skill);
        var cost = Mathf.CeilToInt(GetPurchasingCost(thing, pawn,
            store.ActiveStaff.MaxBy(p => p.skills.GetSkill(SkillDefOf.Social).Level)));

        if (cost > ItemUtility.GetMoney(pawn) * skill * 2) // skill goes from 1 to 20 - money goes from 10 to 60 - so this maxes out at 20*60*2=2400
        {
            return false;
        }

        /*if (ItemUtility.BoughtByPlayer(pawn, thing))
        {
            return false;
        }*/

        //if (thing.IsInValidStorage()) Log.Message(thing.Label + " in storage ");
        return true;
    }

    /*
     *  from rimworld wiki
     * The formula for calculating the buy price modifier(i.e the percentage of the item's value you will pay) is buy price modifier = 60%*(100%+TPI) .
     *  The equivalent formula for the sell price modifier is buy price modifier = 140%*(100%-TPI)
     * Without DLCs the maximum TPI a pawn can have is 50% by having a Social skill of 20 (+30%), a Inspired trade (+18%) and trading with another faction’s settlement (+2%).
     * A TPI of 64.23% is the highest value you can reach in the game with DLCs
     * I took maxTPI of 64.23% -  maxTPI is used to estimate the buying price when the seller is not known
     * 
     *  In contrary to vanilla trade, I used both pawns' TPI : the final TPI  = sellerTPI - buyerTPI
     *  if both are the same the buy price is 72% of the marketvalue (basePriceModifier) which is about how much an average (social skill 14) pawn would get when selling to a trader.
     *  In theory it would be mostly higher, which is the advantage of having a pawn standing buy as a seller.
     *  if you have a lets say a 20 social buyer and 14 social seller, you would get only 65.5% of market value
     *  on the other hand if you have a 5 social buyer and 14 social seller, you would get 85.5% of market value
     *  worst cases:
     *  0 social seller, 28 buyer : 12% market value - you basically give away stuff if you are stupid enough to put an abrasive mute caveman in charge of your store ;)
     *  28 seller, 0 buyer: 90% market value because that is the limit - it creates the opportunity to buy items at 85% from a trader and sell at 90% in your storefront to make a bit of profit :p
     */
    public static float GetPurchasingCost(Thing thing, Pawn buyer, Pawn seller = null)
    {
        thing = thing.GetInnerIfMinified();
        float finalTPIDiff = maxTPI - buyer.GetStatValue(StatDefOf.TradePriceImprovement);
        //Log.Message(buyer.NameShortColored + " buyer TPI= " + buyer.GetStatValue(StatDefOf.TradePriceImprovement));
        if (seller != null)
        {
            //Log.Message(seller.NameShortColored + " seller TPI= " + seller.GetStatValue(StatDefOf.TradePriceImprovement));
            finalTPIDiff = seller.GetStatValue(StatDefOf.TradePriceImprovement) - buyer.GetStatValue(StatDefOf.TradePriceImprovement);
        }
        //Log.Message( " final TPI= " + finalTPIDiff);
        return thing.def.BaseMarketValue * Math.Min(basePriceModifier * (1f + finalTPIDiff), maxPriceModifier);
    }
    
    // Copied from ToilEffects, had to remove Faction check
    public static Toil WithProgressBar(
        this Toil toil,
        TargetIndex ind,
        Func<float> progressGetter,
        bool interpolateBetweenActorAndTarget = false,
        float offsetZ = -0.5f)
    {
        Effecter effecter = null;
        toil.AddPreTickAction(() =>
        {
            //if (toil.actor.Faction != Faction.OfPlayer)
            //    return;
            if (effecter == null)
            {
                effecter = EffecterDefOf.ProgressBar.Spawn();
            }
            else
            {
                LocalTargetInfo target = toil.actor.CurJob.GetTarget(ind);
                if (!target.IsValid || target.HasThing && !target.Thing.Spawned)
                    effecter.EffectTick((TargetInfo) toil.actor, TargetInfo.Invalid);
                else if (interpolateBetweenActorAndTarget)
                    effecter.EffectTick(toil.actor.CurJob.GetTarget(ind).ToTargetInfo(toil.actor.Map), (TargetInfo) toil.actor);
                else
                    effecter.EffectTick(toil.actor.CurJob.GetTarget(ind).ToTargetInfo(toil.actor.Map), TargetInfo.Invalid);
                MoteProgressBar mote = ((SubEffecter_ProgressBar) effecter.children[0]).mote;
                if (mote == null)
                    return;
                mote.progress = Mathf.Clamp01(progressGetter());
                mote.offsetZ = offsetZ;
            }
        });
        toil.AddFinishAction(() =>
        {
            if (effecter == null)
                return;
            effecter.Cleanup();
            effecter = null;
        });
        return toil;
    }
    
    public static bool IsWaitingInQueue(this Pawn pawn)
    {
        if (pawn?.jobs?.curDriver is Shopping.JobDriver_BuyItem buyJob)
            return buyJob.WaitingInQueue;
        else
            return false;
    }
    public static bool IsWaitingToBeServed(this Pawn pawn)
    {
        if (pawn?.jobs?.curDriver is Shopping.JobDriver_BuyItem buyJob)
            return buyJob.WaitingToBeServed;
        else
            return false;
    }
    
    public static void GiveWaitThought(Pawn patron)
    {
        patron.needs.mood?.thoughts.memories.TryGainMemory(ShoppingDefOf.Storefront_HadToWait);
    }
}
