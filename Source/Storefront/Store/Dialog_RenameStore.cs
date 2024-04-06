using Verse;

namespace Storefront.Store
{
    internal class Dialog_RenameStore : Dialog_Rename<StoreController>
    {
        public Dialog_RenameStore(StoreController restaurantController) : base(restaurantController)
        {
        }
        
        public override AcceptanceReport NameIsValid(string name)
        {
            var result = base.NameIsValid(name);
            if (!result.Accepted) return result;
            if (renaming.GetStoresManager().NameIsInUse(name, renaming))
            {
                return "NameIsInUse".Translate();
            }
            return true;
        }        
    }
}