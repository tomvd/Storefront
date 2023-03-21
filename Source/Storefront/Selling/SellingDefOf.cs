using RimWorld;
using Verse;

namespace Storefront.Selling
{
    [DefOf]
    internal static class SellingDefOf
    {
        public static readonly JobDef Storefront_Sell;
        public static readonly JobDef Storefront_StandBy;
        public static readonly WorkTypeDef Storefront_Selling;
        [MayRequire("CashRegister")]
        public static readonly SoundDef CashRegister_Register_Kaching;
    }
}