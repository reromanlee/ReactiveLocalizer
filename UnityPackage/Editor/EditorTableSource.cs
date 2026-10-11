using reromanlee.ReactiveLocalizer.Hosting;
using reromanlee.ReactiveLocalizer.Tables;
using reromanlee.ReactiveLocalizer.Unity;
using UnityEditor;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Delivers catalogs and tables straight from the project's files in the editor, in Play Mode and Edit Mode
    /// alike, so what the editor shows is always what the files say, with no build step in between. Catalogs come
    /// with the pseudo language when it is offered, and its tables are made from the source-language files.
    /// </summary>
    internal sealed class EditorTableSource : ITableSource
    {
        public bool TryLoad(in TableRequest request, TableReceiver receiver)
        {
            IndexedCatalog catalog = CatalogIndex.Find(request.Catalog);
            if (catalog?.Info == null)
            {
                return false;
            }
            if (request.IsCatalog)
            {
                receiver.Receive(CompiledCatalog.Write(PseudoTables.GetCatalog(catalog, PseudoTables.IsOffered)));
                return true;
            }
            if (!catalog.TryGetTable(request.Table, out IndexedTable table))
            {
                return false;
            }
            if (PseudoTables.IsPseudo(catalog, request.Language))
            {
                byte[] pseudo = PseudoTables.Compile(catalog, table, out string problem);
                if (pseudo == null)
                {
                    return false;
                }
                receiver.Warn(problem);
                receiver.Receive(pseudo);
                return true;
            }
            if (!table.TryGetFile(request.Language, out string path))
            {
                return false;
            }
            TableAsset asset = AssetDatabase.LoadAssetAtPath<TableAsset>(path);
            if (asset == null || asset.Data.IsEmpty)
            {
                return false;
            }
            receiver.Receive(asset.Data);
            return true;
        }
    }
}
