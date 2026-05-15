using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Kys_cad_plugin.Core;
using System.Collections.Generic; // HashSet 사용을 위해 추가

namespace Kys_cad_plugin.Commands
{
    public class SfCommand
    {
        // ★ 핵심 수정: CommandFlags.UsePickSet 플래그를 추가하여 '미리 선택' 기능 활성화
        [CommandMethod("sf", CommandFlags.UsePickSet)]
        public void LayerFreezeOthers()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;

            // 마스터 스위치가 꺼져 있는 경우
            if (!CommandSettings.IsPluginEnabled)
            {
                // C# 기능을 종료하고, 기존 LSP의 'c:sf' 함수를 강제로 실행시킵니다.
                doc.SendStringToExecute("(if c:sf (c:sf)) ", true, false, false);
                return;
            }

            Database db = doc.Database;
            Editor ed = doc.Editor;

            // 1. 객체 선택 (UsePickSet 플래그 덕분에 이미 선택된 객체가 있으면 바로 넘어갑니다)
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

                    // 선택된 레이어 리스트에 없고, 현재 작업 중인 레이어(Clayer)가 아닐 경우에만
                    if (!selectedLayers.Contains(ltr.Name) && db.Clayer != layId)
                    {
                        // ★ 끄기(IsOff)는 건드리지 않고, 오직 동결(IsFrozen)만 처리합니다.
                        ltr.IsFrozen = true;
                    }
                }
                tr.Commit();
                ed.WriteMessage($"\n[KYSQL] {selectedLayers.Count}개 레이어를 제외하고 모두 동결했습니다.");
            }
        }
    }
}