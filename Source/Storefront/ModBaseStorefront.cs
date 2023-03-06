using HugsLib;
using Verse;

namespace Storefront
{
    [StaticConstructorOnStartup]
    public class ModBaseStorefront : ModBase
    {
        public override string ModIdentifier => "Storefront";

        private static Settings settings;

        public override void DefsLoaded()
        {
            settings = new Settings(Settings);

            if (GenericDefOf.CashRegister_CashRegister == null)
            {
                GenUI.ErrorDialog("ErrorRequiresCashRegister".Translate());
            }
        }
    }
}