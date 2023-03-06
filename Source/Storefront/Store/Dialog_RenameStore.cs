using Verse;

namespace Storefront.Store
{
    internal class Dialog_RenameStore : Dialog_Rename
    {
        private readonly StoreController Store;

        public Dialog_RenameStore(StoreController Store)
        {
            this.Store = Store;
            curName = Store.Name;
        }

        protected override void SetName(string name)
        {
            Store.Name = name;
        }

        protected override AcceptanceReport NameIsValid(string name)
        {
            var result = base.NameIsValid(name);
            if (!result.Accepted) return result;
            if (Store.GetStoresManager().NameIsInUse(name, Store))
            {
                return "NameIsInUse".Translate();
            }
            return true;
        }
    }
}