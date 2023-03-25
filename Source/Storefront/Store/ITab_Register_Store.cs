using System;
using System.Collections.Generic;
using System.Linq;
using CashRegister;
using HugsLib.Utils;
using RimWorld;
using Storefront.Selling;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Storefront.Store
{
    public class ITab_Register_Store : ITab_Register
    {
        private bool showSettings = true;
        private bool showStats = true;
        private StoreController store;
        private readonly ThingFilterUI.UIState menuFilterState = new ThingFilterUI.UIState();

        public ITab_Register_Store() : base(new Vector2(800, 504))
        {
            labelKey = "TabRegisterStore";
        }

        public override bool IsVisible => true;
        //{
        //    get
        //    {
        //        store = Register.GetStore();
        //        return store != null;
        //    }
        //}

        protected override void FillTab()
        {
            store = Register.GetStore();
            store ??= Register.GetAllStores().First();
            var fullRect = new Rect(0, 16, size.x, size.y - 16);
            var rectLeft = fullRect.LeftHalf().ContractedBy(10f);
            var rectRight = fullRect.RightHalf().ContractedBy(10f);

            DrawLeft(rectLeft);
            DrawRight(rectRight);
        }

        private void DrawRight(Rect rect)
        {
            store ??= Register.GetStore();
            DrawStock(new Rect(rect), store.Stock);
        }

        private void DrawLeft(Rect rect)
        {
            var topBar = rect.TopPartPixels(24);
            DrawStoreSelection(ref topBar);
            rect.yMin += topBar.height;

            if (showSettings)
            {
                var smallRect = new Rect(rect);

                DrawSettings(ref smallRect);
                rect.yMin += smallRect.height + 10;
            }

            if (showStats)
            {
                var smallRect = new Rect(rect);

                DrawStats(ref smallRect);
                rect.yMin += smallRect.height + 10;
            }
        }

        private void DrawStoreSelection(ref Rect rect)
        {
            store ??= Register.GetStore();

            var stores = store.GetStoresManager();
            var rectSelection = rect.LeftHalf();

            Widgets.Label(rectSelection, store.Name.CapitalizeFirst());
            TooltipHandler.TipRegion(rectSelection, "StoreTooltipSelect".Translate());

            var widgetRow = new WidgetRow(rect.xMax, rect.y, UIDirection.LeftThenDown);

            // Rename
            if (widgetRow.ButtonIcon(TexButton.Rename, "StoreTooltipRename".Translate()))
            {
                Find.WindowStack.Add(new Dialog_RenameStore(store));
            }
        }

        private void SetStore(StoreController newStore)
        {
            newStore.LinkRegister(Register);
            store = newStore;
        }

        private void DrawSettings(ref Rect rect)
        {
            const int ListingItems = 5;
            rect.height = 30 * ListingItems;

            var listing = new Listing_Standard();
            listing.Begin(rect);
            {
                listing.CheckboxLabeled("TabRegisterOpened".Translate(), ref store.openForBusiness, "TabRegisterOpenedTooltip".Translate());

                // disabled - for now - the price is calculated in a more complex way than a market value multiplier
                //DrawPrice(listing.GetRect(22));

                //bool guestsPay = store.guestPricePercentage > 0;
                //listing.CheckboxLabeled("TabRegisterGuestsHaveToPay".Translate(), ref guestsPay, "TabRegisterGuestHaveToPayTooltip".Translate(MarketPriceFactor.ToStringPercent()));
                //store.guestPricePercentage = guestsPay ? MarketPriceFactor : 0;
            }
            listing.End();
        }

        private void DrawStats(ref Rect rect)
        {
            const int listingItems = 11;
            rect.height = listingItems * 24 + 20;

            Widgets.DrawBoxSolid(rect, new Color(1f, 1f, 1f, 0.08f));
            rect = rect.ContractedBy(10);
            var listing = new Listing_Standard();
            listing.Begin(rect);
            {
                var activeStaff = store.ActiveStaff;
                var customers = store.Customers;

                listing.LabelDouble("TabRegisterActiveStaff".Translate(), activeStaff.FirstOrDefault()?.LabelShort);
                listing.LabelDouble("TabRegisterCustomers".Translate(), customers.Count.ToString(), customers.Select(p=>p.LabelShort).ToCommaList());
                
                listing.LabelDouble("TabRegisterEarnedYesterday".Translate(), store.incomeYesterday.ToStringMoney());
                listing.LabelDouble("TabRegisterEarnedToday".Translate(), store.incomeToday.ToStringMoney());

            }
            listing.End();
        }

        private static void DrawStock(Rect rect,  IReadOnlyCollection<Thing> stock)
        {
            var grouped = stock.GroupBy(s => s.def);

            var rectImage = new Rect(rect);
            rectImage.height = 20;
            var column = 0;
            var columnwidth = 200;

            // Icons for each type of stock
            foreach (var group in grouped)
            {
                if (group.Key == null) continue;
                // Amount label
                string amountText = $" {stock.Where(s => s.def == group.Key).Sum(s => s.stackCount)}x {group.Key.LabelCap}";
                var amountSize = Text.CalcSize(amountText);
                rectImage.width = amountSize.x;

                // Draw label
                Widgets.Label(rectImage, amountText);
                rectImage.x += rectImage.width;
                // Icon
                rectImage.width = rectImage.height;
                DrawDefIcon(rectImage, group.Key, group.Key.LabelCap);
                rectImage.x += rectImage.width;

                // Will it fit?
                if (rectImage.y + rectImage.height > rect.xMax)
                {
                    column++;
                    rectImage.y = rect.y;
                }
                else
                {
                    rectImage.y += rectImage.height;                    
                }                
                rectImage.x = rect.x + (column * columnwidth);
            }
        }

        private static void DrawDefIcon(Rect rect, ThingDef def, string tooltip = null, Action onClicked = null)
        {
            if (tooltip != null)
            {
                TooltipHandler.TipRegion(rect, tooltip);
                Widgets.DrawHighlightIfMouseover(rect);
            }

            GUI.DrawTexture(rect, def.uiIcon);
            if (onClicked != null && Widgets.ButtonInvisible(rect)) onClicked.Invoke();
        }

        private static IEnumerable<SpecialThingFilterDef> HiddenSpecialThingFilters()
        {
            yield return SpecialThingFilterDefOf.AllowFresh;
        }

        public override bool CanAssignToShift(Pawn pawn)
        {
            return pawn.workSettings.WorkIsActive(SellingDefOf.Storefront_Selling);
        }
    }
}
