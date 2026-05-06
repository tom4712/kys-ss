using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace Kys_cad_plugin.Commands
{
    public class QaCommand
    {
        [CommandMethod("qa")]
        public void LayerThawAll()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
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