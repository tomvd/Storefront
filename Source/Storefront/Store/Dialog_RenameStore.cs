using Verse;

namespace Storefront.Store
{
    internal class Dialog_RenameStore : Dialog_Rename
    {
        private readonly StoreController _store;

        public Dialog_RenameStore(StoreController store)
        {
            _store = store;
            curName = store.Name;
        }

        protected override void SetName(string name)
        {
            _store.Name = name;
        }

        protected override AcceptanceReport NameIsValid(string name)
        {
            var result = base.NameIsValid(name);
            if (!result.Accepted) return result;
            if (_store.GetStoresManager().NameIsInUse(name, _store))
            {
                return "NameIsInUse".Translate();
            }
            return true;
        }
    }
}