using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using FoodUtility = RimWorld.FoodUtility;

namespace Storefront.Shopping
{
	public class JobGiver_BuyFood : ThinkNode_JobGiver
	{
        public override float GetPriority(Pawn pawn)
		{
			if (!HospitalityCompat.IsGuest(pawn)) return 0;
			if (pawn.skills.GetSkill(SkillDefOf.Social).TotallyDisabled) return 0;

			var need = pawn.needs.food;
			if (need == null) return 0;

			if ((int) pawn.needs.food.CurCategory < 3 && FoodUtility.ShouldBeFedBySomeone(pawn)) return 0;

			var workerChance = ShoppingDefOf.Storefront_BuyFood.Worker.GetChance(pawn) / ShoppingDefOf.Storefront_BuyFood.Worker.def.baseChance;

			var requiresFoodFactor = HospitalityCompat.GetRequiresFoodFactor(pawn);
			if (requiresFoodFactor > 0.35f)
			{
				return requiresFoodFactor * 6;
			}
			var priority = requiresFoodFactor * workerChance;
			return priority;
		}

        public override Job TryGiveJob(Pawn pawn)
		{
			if (pawn.needs.food == null) return null;

			if (ShoppingDefOf.Storefront_BuyFood.Worker.MissingRequiredCapacity(pawn) != null) return null;

			return ShoppingDefOf.Storefront_BuyFood.Worker.TryGiveJob(pawn);
		}
	}
}
