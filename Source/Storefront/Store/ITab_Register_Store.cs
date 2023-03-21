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
            // Inventory ?
            {
                //var menuRect = new Rect(rect);
                //menuRect.yMax -= 36;

                //store.Menu.GetMenuFilters(out var filter, out var parentFilter);
                //ThingFilterUI.DoThingFilterConfigWindow(menuRect, menuFilterState, filter, parentFilter, 1, null, HiddenSpecialThingFilters(), true);
            }
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

            // Select
            /*
            if (Widgets.ButtonText(rectSelection, store.Name.CapitalizeFirst()))
            {
                var list = new List<FloatMenuOption>();
                foreach (var controllerOption in stores.stores)
                {
                    list.Add(new FloatMenuOption(controllerOption.Name.CapitalizeFirst(), delegate
                    {
                        SetStore(controllerOption);
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(list));
            }*/
            Widgets.Label(rectSelection, store.Name.CapitalizeFirst());
            TooltipHandler.TipRegion(rectSelection, "StoreTooltipSelect".Translate());

            var widgetRow = new WidgetRow(rect.xMax, rect.y, UIDirection.LeftThenDown);

            // Remove
            /*if (stores.stores.Count > 1)
                if (widgetRow.ButtonIcon(TexButton.Minus, "StoreTooltipRemove".Translate()))
                {
                    Find.WindowStack.Add(new Dialog_Confirm("StoreDialogConfirmRemove".Translate(store.Name),
                        () =>
                        {
                            stores.DeleteStore(store);
                            SetStore(stores.stores.Last());
                        }, true));
                }
            // Add
            if (widgetRow.ButtonIcon(TexButton.Plus, "StoreTooltipAdd".Translate()))
            {
                SetStore(stores.AddStore());
            }*/
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

                DrawPrice(listing.GetRect(22));

                //bool guestsPay = store.guestPricePercentage > 0;
                //listing.CheckboxLabeled("TabRegisterGuestsHaveToPay".Translate(), ref guestsPay, "TabRegisterGuestHaveToPayTooltip".Translate(MarketPriceFactor.ToStringPercent()));
                //store.guestPricePercentage = guestsPay ? MarketPriceFactor : 0;
            }
            listing.End();
        }

        private void DrawPrice(Rect rect)
        {
            // Price
            var price = store.guestPricePercentage <= 0? (string)"TabRegisterGuestPriceFree".Translate() : store.guestPricePercentage.ToStringPercentEmptyZero();
            var label = "TabRegisterGuestPrice".Translate(price);
            var value = Widgets.HorizontalSlider(rect, store.guestPricePercentage, 0, 5, false, label, null, null, 0.1f);
            TooltipHandler.TipRegionByKey(rect, "TabRegisterGuestPriceTooltip");

            if (value != store.guestPricePercentage)
            {
                SoundDefOf.DragSlider.PlayOneShotOnCamera();
                store.guestPricePercentage = value;
            }
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
                var patrons = store.Patrons;
                //var stock = store.Stock.AllStock;

                listing.LabelDouble("TabRegisterActiveStaff".Translate(), activeStaff.FirstOrDefault()?.LabelShort);
                listing.LabelDouble("TabRegisterPatrons".Translate(), patrons.Count.ToString(), patrons.Select(p=>p.LabelShort).ToCommaList());
                //DrawStock(listing, "TabRegisterStocked".Translate(), stock, store);
                listing.LabelDouble("TabRegisterEarnedYesterday".Translate(), store.incomeYesterday.ToStringMoney());
                listing.LabelDouble("TabRegisterEarnedToday".Translate(), store.incomeToday.ToStringMoney());

                //listing.LabelDouble("TabRegisterStocked".Translate(), stock.Sum(s=>s.stackCount).ToString(), stock.Select(s=>s.def).Distinct().Select(s=>s.label).ToCommaList());
            }
            listing.End();
        }

/*
        private static void DrawOrders(Listing listing, TaggedString label,  IReadOnlyCollection<Order> orders)
        {
            // Label
            var rect = CustomLabelDouble(listing, label, $"{orders.Count}: ", out var countSize);

            var grouped = orders.GroupBy(o => o.consumableDef);

            var rectImage  = rect.RightHalf();
            rectImage.xMin += countSize.x;
            rectImage.width = rectImage.height = countSize.y;

            // Icons for each type of order
            foreach (var group in grouped)
            {
                if (group.Key == null) continue;
                // A list of the patrons for the order
                DrawDefIcon(rectImage, group.Key, $"{group.Key.LabelCap}: {group.Select(o => o.patron.Name.ToStringShort).ToCommaList()}");
                rectImage.x += 2 + rectImage.width;
               
                // Will the next one fit?
                if (rectImage.xMax > rect.xMax) break;
            }
            listing.Gap(listing.verticalSpacing);
        }*/

        private static void DrawStockExpanded(Listing listing, TaggedString label,  IReadOnlyCollection<Thing> stock)
        {
            // Label
            var rect = CustomLabelDouble(listing, label, $"{stock.Count}:", out var countSize);

            var grouped = stock.GroupBy(s => s.def);

            var rectImage  = rect.RightHalf();
            rectImage.xMin += countSize.x;
            rectImage.height = countSize.y;

            // Icons for each type of stock
            foreach (var group in grouped)
            {
                if (group.Key == null) continue;
                // Amount label
                string amountText = $" {group.Count()}x";
                var amountSize = Text.CalcSize(amountText);
                rectImage.width = amountSize.x;

                // Will it fit?
                if (rectImage.xMax + rectImage.height > rect.xMax) break;

                // Draw label
                Widgets.Label(rectImage, amountText);
                rectImage.x += rectImage.width;
                // Icon
                rectImage.width = rectImage.height;
                DrawDefIcon(rectImage, group.Key, group.Key.LabelCap);
                rectImage.x += rectImage.width;

                // Will the next one fit?
                if (rectImage.xMax > rect.xMax) break;
            }
            listing.Gap(listing.verticalSpacing);
        }

        /*private static void DrawStock(Listing listing, TaggedString label,  IReadOnlyDictionary<ThingDef, StoreStock.Stock> stock, StoreController store)
        {
            // Label
            var rect = CustomLabelDouble(listing, label, $"{stock.Values.Sum(pair => pair.items.Sum(item=>item.stackCount))}", out var countSize);

            var rectIcon  = rect.RightHalf();
            //rectIcon.xMin += countSize.x;
            var iconSize = rectIcon.width = rectIcon.height = countSize.y;
            
            var iconCols = Mathf.FloorToInt(rect.width / iconSize);
            var iconRows = Mathf.CeilToInt((float) stock.Count / iconCols);
            var height = iconRows * iconSize;

            var rectIcons = listing.GetRect(height);
            rectIcon.x = rectIcons.x;
            rectIcon.y = rectIcons.y;

            // Icons for each type of stock
            int col = 0;
            foreach (var group in stock.Values)
            {
                if (group.def == null) continue;
                if (group.items.Count == 0) continue;

                // Icon
                var value = group.def.GetPrice(store).ToStringMoney();
                DrawDefIcon(rectIcon, group.def, $"{group.items.Sum(item => item.stackCount)}x {group.def.LabelCap} ({value})");
                rectIcon.x += iconSize;

                col++;
                if (col == iconCols)
                {
                    col = 0;
                    rectIcon.x = rectIcons.x;
                }
            }
            listing.Gap(listing.verticalSpacing);
        }

        private static void DrawDebts(Listing listing, TaggedString label,  ReadOnlyCollection<Debt> debts)
        {
            // Label
            var rect = CustomLabelDouble(listing, label, $"{debts.Sum(debt => debt.amount).ToStringMoney()}", out var countSize);

            var rectIcon  = rect.RightHalf();
            //rectIcon.xMin += countSize.x;
            var iconSize = rectIcon.width = rectIcon.height = countSize.y;
            
            var iconCols = Mathf.FloorToInt(rect.width / iconSize);
            var iconRows = Mathf.CeilToInt((float) debts.Count / iconCols);
            var height = iconRows * iconSize;

            var rectIcons = listing.GetRect(height);
            rectIcon.x = rectIcons.x;
            rectIcon.y = rectIcons.y;

            // Icons for each type of stock
            int col = 0;
            foreach (var debt in debts)
            {
                if (debt.patron == null) continue;
                if (debt.amount <= 0) continue;

                // Icon
                DrawDefIcon(rectIcon, ThingDefOf.Silver, "TabRegisterDebtTooltip".Translate(debt.patron.Name.ToStringFull, debt.amount.ToStringMoney()), () => debt.amount = 0);
                rectIcon.x += iconSize;

                col++;
                if (col == iconCols)
                {
                    col = 0;
                    rectIcon.x = rectIcons.x;
                }
            }

            listing.Gap(listing.verticalSpacing);
        }*/

        private static Rect CustomLabelDouble(Listing listing, TaggedString labelLeft, TaggedString stringRight, out Vector2 sizeRight)
        {
            sizeRight = Text.CalcSize(stringRight);
            Rect rect = listing.GetRect(Mathf.Max(Text.CalcHeight(labelLeft, listing.ColumnWidth / 2f), sizeRight.y));
            Widgets.Label(rect.LeftHalf(), labelLeft);
            Widgets.Label(rect.RightHalf(), stringRight);
            return rect;
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
