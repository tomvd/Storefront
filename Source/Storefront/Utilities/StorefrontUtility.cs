using System;
using Hospitality;
using JetBrains.Annotations;
using RimWorld;
using Storefront.Shopping;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Storefront.Utilities;

public static class StorefrontUtility
{
    public static float maxTPI = 0.6423f;
    public static float basePriceModifier = 0.72f;
    public static float maxPriceModifier = 0.90f;
    
    public static bool IsBuyableAtAll(Pawn pawn, Thing thing)
    {
        if (thing.def.isUnfinishedThing) return false;

        if (thing.def == ThingDefOf.Silver) return false;

        if (!pawn.MayPurchaseThing(thing)) return false;

        if (thing.def.thingSetMakerTags != null && thing.def.thingSetMakerTags.Contains("NotForGuests")) return false;

        if (!ItemUtility.IsBuyableNow(pawn, thing)) return false;
        //if (!thing.IsSociallyProper(pawn))
        //{
        //    Log.Message(thing.Label + ": is not proper for " + pawn.NameStringShort);
        //    return false;
        //}
        var cost = Mathf.CeilToInt(GetPurchasingCost(thing, pawn));

        if (cost > ItemUtility.GetMoney(pawn))
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
     *  In contrary to vanilla trade, to make I used both pawns' TPI : the final TPI  = sellerTPI - buyerTPI
     *  if both are the same the buy price is 72% of the marketvalue (basePriceModifier) which is about how much an average (social skill 14) pawn would get when selling to a trader.
     *  In theory it would be mostly higher, unless you got a highly skilled buyer, which is the tradeoff of having a pawn standing buy as a seller.
     *  if you have a lets say a 20 social buyer and 14 social seller, you would get only 65.5% of market value
     *  on the other hand if you have a 5 social buyer and 14 social seller, you would get 85.5% of market value
     *  worst cases:
     *  0 social seller, 28 buyer : 12% market value - you basically give away stuff if you are stupid enough to put an abrasive mute caveman in charge of your store ;)
     *  28 seller, 0 buyer: 90% market value because that is the limit - it creates the opportunity to buy items at 85% from a trader and sell at 90% in your storefront to make a bit of profit :p
     */
    public static float GetPurchasingCost(Thing thing, Pawn buyer, Pawn seller = null)
    {
        float finalTPIDiff = maxTPI - buyer.GetStatValue(StatDefOf.TradePriceImprovement);
        Log.Message(buyer.NameShortColored + " buyer TPI= " + buyer.GetStatValue(StatDefOf.TradePriceImprovement));
        if (seller != null)
        {
            Log.Message(seller.NameShortColored + " seller TPI= " + seller.GetStatValue(StatDefOf.TradePriceImprovement));
            finalTPIDiff = seller.GetStatValue(StatDefOf.TradePriceImprovement) - buyer.GetStatValue(StatDefOf.TradePriceImprovement);
        }
        Log.Message( " final TPI= " + finalTPIDiff);
        return thing.MarketValue * Math.Min(basePriceModifier * (1f + finalTPIDiff), maxPriceModifier);
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
    
    public static CustomerState GetCustomerState(this Pawn pawn)
    {
        if (pawn?.jobs?.curDriver is Storefront.Shopping.JobDriver_BuyItem buyJob)
            return buyJob.CustomerState;
        else
            return CustomerState.Leaving;
    }
    
    public static void GiveWaitThought(Pawn patron)
    {
        // TODO
        //patron.needs.mood?.thoughts.memories.TryGainMemory(StorefrontDefOf.Gastronomy_HadToWait);
    }
}
