using UnityEngine;
using Verse;

namespace Storefront;

public class Settings : ModSettings
{
    /// <summary>
    /// When true, colonists assigned to stand-by at a cash register will skip that job
    /// unless at least one friendly visitor or guest is currently on the map.
    /// </summary>
    public bool StandByOnlyWhenCustomersAround;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref StandByOnlyWhenCustomersAround, "standByOnlyWhenCustomersAround", true);
        base.ExposeData();
    }

    public void DoWindowContents(Rect inRect)
    {
        Listing_Standard listing = new Listing_Standard();
        listing.Begin(inRect);
        listing.CheckboxLabeled(
            "SettingStandByOnlyWhenCustomers".Translate(),
            ref StandByOnlyWhenCustomersAround,
            "SettingStandByOnlyWhenCustomersTooltip".Translate());
        listing.End();
    }
}
