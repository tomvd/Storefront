using System;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Storefront.Shopping
{
    public enum ShoppingCategory
    {
        Food,
        Drugs,
        Apparel,
        Weapons,
        Furniture,
        Misc
    }

    /// <summary>
    /// Gives every visiting pawn a random preferred shopping category, so purchases are
    /// spread across the store's whole range instead of clustering on whatever scores
    /// highest (typically weapons, armor or food). Shoppers first try to find something
    /// they like within their preferred category and only then fall back to the full range.
    ///
    /// The preference is seeded per pawn and per visit (lord), so it is stable for the
    /// whole visit, but the same guest can come back another day wanting something else.
    /// </summary>
    public static class ShoppingPreferences
    {
        public static ShoppingCategory GetPreferredCategory(Pawn pawn)
        {
            var categories = (ShoppingCategory[]) Enum.GetValues(typeof(ShoppingCategory));
            int seed = pawn.thingIDNumber * 60509 ^ (pawn.GetLord()?.loadID ?? 0) * 33151;
            return categories[Rand.RangeSeeded(0, categories.Length, seed)];
        }

        public static ShoppingCategory GetCategory(Thing thing)
        {
            var def = thing.GetInnerIfMinified().def;
            // Drugs first: recreational drugs like beer also give nutrition, and joy-only
            // ingestibles (e.g. chocolate is food, smokeleaf joints are not) count as drugs too.
            if (def.IsDrug || def.ingestible != null && def.ingestible.joy > 0 && !def.IsNutritionGivingIngestible)
                return ShoppingCategory.Drugs;
            if (def.IsNutritionGivingIngestible) return ShoppingCategory.Food;
            if (def.IsApparel) return ShoppingCategory.Apparel;
            if (def.IsWeapon) return ShoppingCategory.Weapons;
            if (def.Minifiable || def.IsArt) return ShoppingCategory.Furniture;
            return ShoppingCategory.Misc;
        }
    }
}
