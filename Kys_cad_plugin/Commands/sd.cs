using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Kys_cad_plugin.Core;

namespace Kys_cad_plugin.Commands
{
    public class SdCommand
    {
        [CommandMethod("sd")]
        public void LayerFreezeSelected()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;

            // 마스터 스위치가 꺼져 있는 경우
            if (!CommandSettings.IsPluginEnabled)
            {
                // ★ 핵심: C# 기능을 종료하고, 기존 LSP의 'c:sf' 함수를 강제로 실행시킵니다.
                // (if c:sf ...) 구문을 통해 해당 리습이 로드되어 있는지 확인 후 실행하여 에러를 방지합니다.
                doc.SendStringToExecute("(if c:sd (c:sd)) ", true, false, false);
                return;
            }
            Database db = doc.Database;
            Editor ed = doc.Editor;

            PromptSelectionResult selRes = ed.GetSelection();
            if (selRes.Status != PromptStatus.OK) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                HashSet<string> layersToFreeze = new HashSet<string>();

                foreach (SelectedObject selObj in selRes.Value)
                {
                    Entity ent = (Entity)tr.GetObject(selObj.ObjectId, OpenMode.ForRead);
                    layersToFreeze.Add(ent.Layer);
                }

                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId layId in lt)
                {
                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(layId, OpenMode.ForWrite);

                    // 선택된 레이어에 포함되어 있고, 현재 레이어가 아닐 경우 동결
                    if (layersToFreeze.Contains(ltr.Name))
                    {
                        if (db.Clayer == layId)
                        {
                            ed.WriteMessage($"\n[KYSQL경고] 현재 레이어({ltr.Name})는 동결할 수 없습니다.");
                            continue;
                        }
                        ltr.IsFrozen = true;
                    }
                }
                tr.Commit();
                ed.WriteMessage("\n[KYSQL] 선택한 객체의 레이어를 동결했습니다.");
            }
        }
    }
}