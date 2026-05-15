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
                // C# 기능을 종료하고, 기존 LSP의 'c:qa' 함수를 강제로 실행시킵니다.
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

                    // ★ 수정된 부분: '끄기(IsOff)' 조건은 무시하고 오직 '동결(IsFrozen)' 상태만 확인 후 해제합니다.
                    if (ltr.IsFrozen)
                    {
                        ltr.IsFrozen = false; // 동결 해제
                        // ltr.IsOff = false; // <- 이 부분을 제거하여 켜기/끄기 상태는 건드리지 않음
                        count++;
                    }
                }

                tr.Commit();

                // 화면 갱신: 동결 해제 후 객체를 다시 그리기 위해 필수
                ed.Regen();

                // 메시지도 동결 해제에 맞게 수정
                ed.WriteMessage($"\n[KYSQL] {count}개 레이어의 동결을 해제했습니다.");
            }
        }
    }
}