using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Kys_cad_plugin.Core;
using System.Collections.Generic; // HashSet 사용을 위해 추가

namespace Kys_cad_plugin.Commands
{
    public class SdCommand
    {
        // ★ 핵심 수정: CommandFlags.UsePickSet 플래그를 추가해야 '선택 후 명령어 입력'이 작동합니다.
        [CommandMethod("sd", CommandFlags.UsePickSet)]
        public void LayerFreezeSelected()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;

            // 마스터 스위치가 꺼져 있는 경우
            if (!CommandSettings.IsPluginEnabled)
            {
                // C# 기능을 종료하고, 기존 LSP의 'c:sd' 함수를 강제로 실행시킵니다.
                doc.SendStringToExecute("(if c:sd (c:sd)) ", true, false, false);
                return;
            }

            Database db = doc.Database;
            Editor ed = doc.Editor;

            // ★ UsePickSet 플래그 덕분에, 객체가 이미 선택되어 있으면 바로 넘어가고 
            // 선택된 게 없을 때만 객체를 선택하라는 프롬프트를 띄웁니다.
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

                    // 선택된 레이어에 포함되어 있고, 현재 레이어가 아닐 경우
                    if (layersToFreeze.Contains(ltr.Name))
                    {
                        if (db.Clayer == layId)
                        {
                            ed.WriteMessage($"\n[KYSQL경고] 현재 레이어({ltr.Name})는 동결할 수 없습니다.");
                            continue;
                        }

                        // ★ 끄기(IsOff)는 건드리지 않고, 오직 동결(IsFrozen)만 처리합니다.
                        ltr.IsFrozen = true;
                    }
                }
                tr.Commit();
                ed.WriteMessage("\n[KYSQL] 선택한 객체의 레이어를 동결했습니다.");
            }
        }
    }
}