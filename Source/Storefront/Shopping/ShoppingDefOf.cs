using RimWorld;
using Verse;

namespace Storefront.Shopping
{
    [DefOf]
    internal static class ShoppingDefOf
    {
        public static readonly JobDef Storefront_BrowseItems;
        public static readonly JobDef Storefront_BuyItem;
        public static JoyGiverDef Storefront_BuyFood;
        public static readonly ThoughtDef Storefront_Serviced;
        public static readonly ThoughtDef Storefront_ServicedMood;
        public static readonly ThoughtDef Storefront_HadToWait;        
    }
}