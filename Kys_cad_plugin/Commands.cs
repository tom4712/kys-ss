using Autodesk.AutoCAD.Runtime;
using Kys_cad_plugin.Core;

namespace Kys_cad_plugin
{
    public class Commandss
    {
        [CommandMethod("KYSQL_LOAD")]
        public void LoadKysUI()
        {
            PaletteManager.Show();
        }
    }
}