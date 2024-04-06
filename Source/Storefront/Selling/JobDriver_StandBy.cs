using System.Collections.Generic;
using Verse.AI;

namespace Storefront.Selling
{
	public class JobDriver_StandBy : JobDriver
	{
		private const TargetIndex IndexRegister = TargetIndex.A;
		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		public override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(IndexRegister);
			this.FailOnForbidden(IndexRegister);
			yield return Toils_Goto.GotoThing(IndexRegister, PathEndMode.InteractionCell);
			yield return Toils_Selling.WaitForBetterJob(IndexRegister);
		}
	}
}
