using System;
using System.Linq;
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
        if (thing.def.Minifiable)
        {
            if (!(thing is MinifiedThing))
            {
                return false;
            }
        }
        thing = thing.GetInnerIfMinified();

        if (!store.IsForSale(thing.def))
        {
            return false;
        }

        if (thing.def.isUnfinishedThing)
        {
            return false;
        }

        if (thing.def == ThingDefOf.Silver) return false;

        if (thing.def.thingSetMakerTags != null && thing.def.thingSetMakerTags.Contains("NotForGuests"))
        {
            return false;
        }

        if (!thing.SpawnedOrAnyParentSpawned)
        {
            return false;
        }

        if (thing.ParentHolder is Pawn)
        {
            return false;
        }

        if (thing.IsForbidden(Faction.OfPlayer))
        {
            return false;
        }

        if (pawn == null) return true; // shortcut here, we are not checking for a specific pawn

        // Put all pawn checks below here

        if (!pawn.HasReserved(thing) && !pawn.CanReserve(thing))
        {
            return false;
        }

        // we actually might want to buy something above our budget if the skill of the seller is high enough
        var bestSeller = store.ActiveStaff.MaxBy(p => p.skills.GetSkill(SkillDefOf.Social).Level);
        if (bestSeller == null) return false; // store has no active staff — nothing to sell

        float skill = bestSeller.skills.GetSkill(SkillDefOf.Social).Level;
        var cost = Mathf.CeilToInt(GetPurchasingCost(thing, pawn, bestSeller));

        if (cost > HospitalityCompat.GetMoney(pawn) * skill * 2) // skill goes from 1 to 20 - money goes from 10 to 60 - so this maxes out at 20*60*2=2400
        {
            return false;
        }

        return true;
    }

    /*
     *  from rimworld wiki
     * The formula for calculating the buy price modifier(i.e the percentage of the item's value you will pay) is buy price modifier = 60%*(100%+TPI) .
     *  The equivalent formula for the sell price modifier is buy price modifier = 140%*(100%-TPI)
     * Without DLCs the maximum TPI a pawn can have is 50% by having a Social skill of 20 (+30%), a Inspired trade (+18%) and trading with another faction's settlement (+2%).
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
     *
     *  On top of all that, the storyteller's "Trade price disadvantage" custom difficulty setting
     *  is applied (the store selling to a guest is a player sale), so store prices stay in line
     *  with what a trade caravan would pay under the same setting.
     */
    public static float GetPurchasingCost(Thing thing, Pawn buyer, Pawn seller = null)
    {
        thing = thing.GetInnerIfMinified();
        float buyerTPI = GetTPI(buyer);
        float finalTPIDiff = maxTPI - buyerTPI;
        if (seller != null)
        {
            finalTPIDiff = GetTPI(seller) - buyerTPI;
        }
        var price = thing.MarketValue * Math.Min(basePriceModifier * (1f + finalTPIDiff), maxPriceModifier);
        // A store sale is a player sale: respect the storyteller's "trade price disadvantage"
        // setting, exactly like vanilla applies it in TradeUtility/Tradeable for caravan trades.
        price *= 1f - (Find.Storyteller?.difficulty?.tradePriceFactorLoss ?? 0f);
        return price;
    }

    /// <summary>
    /// Returns the pawn's TradePriceImprovement stat value, or 0 when the stat is
    /// disabled for this pawn (e.g. certain modded races or capacity-disabled pawns).
    /// Calling GetStatValue on a disabled stat logs an ErrorOnce spam in vanilla.
    /// </summary>
    private static float GetTPI(Pawn pawn)
    {
        var stat = StatDefOf.TradePriceImprovement;
        return stat.Worker.IsDisabledFor(pawn) ? 0f : pawn.GetStatValue(stat);
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
