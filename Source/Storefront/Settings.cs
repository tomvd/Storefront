using UnityEngine;
using Verse;

namespace Storefront;

public class Settings : ModSettings
{
    public bool MassCasualties;
    
    public override void ExposeData()
    {
        //Scribe_Values.Look(ref MassCasualties, "massCasualties", false);
        base.ExposeData();
    }

    public void DoWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new Listing_Standard();
        listingStandard.Begin(inRect);
        //listingStandard.CheckboxLabeled("MassCasualties".Translate(), ref MassCasualties, "MassCasualtiesTooltip".Translate());
        listingStandard.End();
    }
}