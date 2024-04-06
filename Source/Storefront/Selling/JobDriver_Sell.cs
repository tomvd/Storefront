using System.Collections.Generic;
using Storefront.Shopping;
using Verse;
using Verse.AI;

namespace Storefront.Selling
{
    public class JobDriver_Sell : JobDriver
    {
        private TargetIndex CustomerInd = TargetIndex.A;
        private TargetIndex ItemInd = TargetIndex.B;
        private TargetIndex RegisterInd = TargetIndex.C;
        private Pawn Customer => job.GetTarget(CustomerInd).Pawn;
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if ((Customer.jobs.curDriver as JobDriver_BuyItem) == null)
            {
                Log.Message($"{Customer.NameShortColored} is not buying anything anymore.");
                return false;
            }
            return true;
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(ItemInd);
            this.FailOnForbidden(ItemInd);
            this.FailOnDowned(CustomerInd);
            yield return Toils_Goto.GotoThing(RegisterInd, PathEndMode.InteractionCell);
            yield return Toils_Selling.AnnounceSelling(CustomerInd, ItemInd);
            yield return Toils_General.Wait(100, CustomerInd).FailOnDowned(CustomerInd);
            yield return Toils_Selling.serveCustomer(CustomerInd, ItemInd, RegisterInd).PlaySoundAtStart(SellingDefOf.CashRegister_Register_Kaching);
        }
    }
}
