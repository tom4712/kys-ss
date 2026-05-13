using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Kys_cad_plugin.Core;

namespace Kys_cad_plugin.Commands
{
    public class QaCommand
    {
        [CommandMethod("qa")]
        public void LayerThawAll()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;

            // 마스터 스위치가 꺼져 있는 경우
            if (!CommandSettings.IsPluginEnabled)
            {
                // ★ 핵심: C# 기능을 종료하고, 기존 LSP의 'c:sf' 함수를 강제로 실행시킵니다.
                // (if c:sf ...) 구문을 통해 해당 리습이 로드되어 있는지 확인 후 실행하여 에러를 방지합니다.
                doc.SendStringToExecute("(if c:qa (c:qa)) ", true, false, false);
                return;
            }
            
            if (doc == null) return;

            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                int count = 0;

                foreach (ObjectId layId in lt)
                {
                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(layId, OpenMode.ForWrite);

                    // 동결 및 끄기 상태 모두 해제
                    if (ltr.IsFrozen || ltr.IsOff)
                    {
                        ltr.IsFrozen = false;
                        ltr.IsOff = false;
                        count++;
                    }
                }

                tr.Commit();

                // 화면 갱신: 동결 해제 후 객체를 다시 그리기 위해 필수
                ed.Regen();

                ed.WriteMessage($"\n[KYSQL] {count}개 레이어의 동결 및 숨김을 해제했습니다.");
            }
        }
    }
}