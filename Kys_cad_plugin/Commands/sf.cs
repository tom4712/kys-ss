using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Kys_cad_plugin.Core;

namespace Kys_cad_plugin.Commands
{
    public class SfCommand
    {
        [CommandMethod("sf")]
        public void LayerFreezeOthers()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;

            // 마스터 스위치가 꺼져 있는 경우
            if (!CommandSettings.IsPluginEnabled)
            {
                // ★ 핵심: C# 기능을 종료하고, 기존 LSP의 'c:sf' 함수를 강제로 실행시킵니다.
                // (if c:sf ...) 구문을 통해 해당 리습이 로드되어 있는지 확인 후 실행하여 에러를 방지합니다.
                doc.SendStringToExecute("(if c:sf (c:sf)) ", true, false, false);
                return;
            }
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // 1. 객체 선택
            PromptSelectionResult selRes = ed.GetSelection();
            if (selRes.Status != PromptStatus.OK) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                HashSet<string> selectedLayers = new HashSet<string>();

                // 2. 선택된 객체들의 레이어 이름 수집
                foreach (SelectedObject selObj in selRes.Value)
                {
                    Entity ent = (Entity)tr.GetObject(selObj.ObjectId, OpenMode.ForRead);
                    selectedLayers.Add(ent.Layer);
                }

                // 3. 레이어 테이블 순회하며 동결 처리
                foreach (ObjectId layId in lt)
                {
                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(layId, OpenMode.ForWrite);

                    // 선택된 레이어 리스트에 없고, 현재 레이어가 아닐 경우에만 동결
                    if (!selectedLayers.Contains(ltr.Name) && db.Clayer != layId)
                    {
                        ltr.IsFrozen = true;
                    }
                }
                tr.Commit();
                ed.WriteMessage($"\n[KYSQL] {selectedLayers.Count}개 레이어를 제외하고 모두 동결했습니다.");
            }
        }
    }
}