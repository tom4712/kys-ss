using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Wpf.Ui.Controls;

// 오토캐드 API
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

// 좌표 변환 API (ProjNet & GeoAPI)
using GeoAPI.CoordinateSystems;
using GeoAPI.CoordinateSystems.Transformations;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace Kys_cad_plugin.Views
{
    public partial class DwgCoordUI : FluentWindow
    {
        // 원본 코드의 좌표계 딕셔너리
        private static readonly Dictionary<string, string> WktDict = new Dictionary<string, string>
        {
            { "서해", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"scale_factor\",1],PARAMETER[\"central_meridian\",123],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",600000],UNIT[\"Meter\",1]]" },
            { "서부", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",125],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",600000],UNIT[\"Meter\",1]]" },
            { "중부", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",127],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",600000],UNIT[\"Meter\",1]]" },
            { "동부", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",129],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",600000],UNIT[\"Meter\",1]]" },
            { "동해", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",131],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",600000],UNIT[\"Meter\",1]]" },
            { "UTMK", "PROJCS[\"PCS_ITRF2000_TM\",GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS84\",SPHEROID[\"WGS84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"Degree\",0.017453292519943295]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",127.5],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",1000000],PARAMETER[\"false_northing\",2000000],UNIT[\"Meter\",1]]" },
            { "UTM52N", "PROJCS[\"UTM_Zone_52_Northern_Hemisphere\",GEOGCS[\"GCS_Geographic Coordinate System\",DATUM[\"D_WGS84\",SPHEROID[\"WGS84\",6378137,298.257223560493]],PRIMEM[\"Greenwich\",0],UNIT[\"Degree\",0.017453292519943295]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",129],PARAMETER[\"scale_factor\",0.9996],PARAMETER[\"false_easting\",500000],PARAMETER[\"false_northing\",0],UNIT[\"Meter\",1]]" },
            { "경위도", "GEOGCS[\"Geographic Coordinate System\",DATUM[\"D_WGS84\",SPHEROID[\"WGS84\",6378137,298.257223560493]],PRIMEM[\"Greenwich\",0],UNIT[\"Degree\",0.017453292519943295]]" },
            { "서해(50만)", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",123],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",500000],UNIT[\"Meter\",1]]" },
            { "서부(50만)", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",125],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",500000],UNIT[\"Meter\",1]]" },
            { "중부(50만)", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",127],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",500000],UNIT[\"Meter\",1]]" },
            { "동부(50만)", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",129],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",500000],UNIT[\"Meter\",1]]" },
            { "동해(50만)", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",131],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",500000],UNIT[\"Meter\",1]]" }
        };

        public DwgCoordUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

            InitUI();
        }

        private void InitUI()
        {
            // 도면 리스트 바인딩
            CboDrawingSelect.Items.Clear();
            foreach (Document doc in CadApp.DocumentManager)
            {
                string activeTag = (doc == CadApp.DocumentManager.MdiActiveDocument) ? " (현재)" : "";
                CboDrawingSelect.Items.Add(new DrawingItem { DisplayName = $"📄 {doc.Name}{activeTag}", Doc = doc });
            }
            if (CboDrawingSelect.Items.Count > 0) CboDrawingSelect.SelectedIndex = 0;

            // 좌표계 리스트 바인딩
            var keys = WktDict.Keys.ToArray();
            CboSourceCS.ItemsSource = keys;
            CboTargetCS.ItemsSource = keys;

            // 기본 선택값 세팅
            CboSourceCS.SelectedIndex = 0; // 서해
            CboTargetCS.SelectedIndex = 1; // 서부
        }

        private void AddLog(string message, bool isError = false)
        {
            var tb = new System.Windows.Controls.TextBlock { Text = $"▶ {message}", FontSize = 11, Margin = new Thickness(2) };
            if (isError) tb.Foreground = System.Windows.Media.Brushes.Red;
            else tb.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            LogListBox.Items.Add(tb);
            LogListBox.ScrollIntoView(tb);
        }

        private async void BtnTransform_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = CboDrawingSelect.SelectedItem as DrawingItem;
            if (selectedItem == null || CboSourceCS.SelectedItem == null || CboTargetCS.SelectedItem == null)
            {
                await ShowModernDialog("알림", "도면 및 변환할 좌표계를 모두 선택해주세요.");
                return;
            }

            string sourceKey = CboSourceCS.SelectedItem.ToString();
            string targetKey = CboTargetCS.SelectedItem.ToString();

            if (sourceKey == targetKey)
            {
                await ShowModernDialog("알림", "원본 좌표계와 대상 좌표계가 동일합니다.");
                return;
            }

            Document sourceDoc = selectedItem.Doc;

            try
            {
                AddLog($"변환 시작: {sourceKey} -> {targetKey}");

                // ProjNet 변환기 세팅
                var csFactory = new CoordinateSystemFactory();
                var ctFactory = new CoordinateTransformationFactory();
                var sourceCS = csFactory.CreateFromWkt(WktDict[sourceKey]);
                var targetCS = csFactory.CreateFromWkt(WktDict[targetKey]);
                IMathTransform transform = ctFactory.CreateFromCoordinateSystems(sourceCS, targetCS).MathTransform;

                // 새 도면 생성
                Document newDoc = CadApp.DocumentManager.Add("");
                CadApp.DocumentManager.MdiActiveDocument = newDoc;
                AddLog("새로운 빈 도면 생성 완료. 객체 복사 중...");

                using (DocumentLock newDocLock = newDoc.LockDocument())
                using (Transaction sourceTrans = sourceDoc.Database.TransactionManager.StartTransaction())
                using (Transaction newTrans = newDoc.Database.TransactionManager.StartTransaction())
                {
                    // 1. 원본 객체 ID 수집
                    BlockTable sourceBt = sourceTrans.GetObject(sourceDoc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord sourceBtr = sourceTrans.GetObject(sourceBt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    ObjectIdCollection sourceIds = new ObjectIdCollection();

                    foreach (ObjectId id in sourceBtr)
                    {
                        sourceIds.Add(id);
                    }

                    if (sourceIds.Count == 0)
                    {
                        AddLog("원본 도면의 모델 스페이스에 복사할 객체가 없습니다.", true);
                        return;
                    }

                    // 2. 객체 복사 (WblockCloneObjects)
                    IdMapping mapping = new IdMapping();
                    newDoc.Database.WblockCloneObjects(
                        sourceIds,
                        newDoc.Database.CurrentSpaceId,
                        mapping,
                        DuplicateRecordCloning.Replace,
                        false
                    );

                    AddLog("객체 복사 완료. 좌표계 변환 수식 적용 중...");

                    // 3. 복사된 객체 좌표 변환
                    BlockTable newBt = newTrans.GetObject(newDoc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord newBtr = newTrans.GetObject(newBt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    int successCount = 0;

                    foreach (ObjectId newId in newBtr)
                    {
                        Entity ent = newTrans.GetObject(newId, OpenMode.ForWrite) as Entity;
                        if (ent == null) continue;

                        try
                        {
                            if (ent is Polyline pline)
                            {
                                for (int i = 0; i < pline.NumberOfVertices; i++)
                                {
                                    var pt = pline.GetPoint2dAt(i);
                                    double[] to = transform.Transform(new double[] { pt.X, pt.Y });
                                    pline.SetPointAt(i, new Point2d(to[0], to[1]));
                                }
                            }
                            else if (ent is Line line)
                            {
                                double[] toStart = transform.Transform(new double[] { line.StartPoint.X, line.StartPoint.Y });
                                double[] toEnd = transform.Transform(new double[] { line.EndPoint.X, line.EndPoint.Y });
                                line.StartPoint = new Point3d(toStart[0], toStart[1], 0);
                                line.EndPoint = new Point3d(toEnd[0], toEnd[1], 0);
                            }
                            else if (ent is DBText text)
                            {
                                double[] to = transform.Transform(new double[] { text.Position.X, text.Position.Y });
                                text.Position = new Point3d(to[0], to[1], text.Position.Z);

                                if (text.Justify != AttachmentPoint.BaseLeft)
                                {
                                    double[] toAlign = transform.Transform(new double[] { text.AlignmentPoint.X, text.AlignmentPoint.Y });
                                    text.AlignmentPoint = new Point3d(toAlign[0], toAlign[1], text.AlignmentPoint.Z);
                                    text.AdjustAlignment(text.Database);
                                }
                            }
                            else if (ent is MText mtext)
                            {
                                double[] to = transform.Transform(new double[] { mtext.Location.X, mtext.Location.Y });
                                mtext.Location = new Point3d(to[0], to[1], 0);
                            }
                            else if (ent is Circle circle)
                            {
                                double[] to = transform.Transform(new double[] { circle.Center.X, circle.Center.Y });
                                circle.Center = new Point3d(to[0], to[1], 0);
                            }
                            else if (ent is DBPoint point)
                            {
                                double[] to = transform.Transform(new double[] { point.Position.X, point.Position.Y });
                                point.Position = new Point3d(to[0], to[1], 0);
                            }
                            else if (ent is Polyline3d pl3d)
                            {
                                foreach (ObjectId vId in pl3d)
                                {
                                    PolylineVertex3d vtx = newTrans.GetObject(vId, OpenMode.ForWrite) as PolylineVertex3d;
                                    if (vtx != null)
                                    {
                                        double[] to = transform.Transform(new double[] { vtx.Position.X, vtx.Position.Y });
                                        vtx.Position = new Point3d(to[0], to[1], 0);
                                    }
                                }
                            }
                            else if (ent is BlockReference blockRef)
                            {
                                double[] to = transform.Transform(new double[] { blockRef.Position.X, blockRef.Position.Y });
                                blockRef.Position = new Point3d(to[0], to[1], blockRef.Position.Z);

                                foreach (ObjectId attId in blockRef.AttributeCollection)
                                {
                                    if (!attId.IsValid || attId.IsErased) continue;
                                    AttributeReference att = newTrans.GetObject(attId, OpenMode.ForWrite) as AttributeReference;
                                    if (att != null)
                                    {
                                        double[] toAtt = transform.Transform(new double[] { att.Position.X, att.Position.Y });
                                        att.Position = new Point3d(toAtt[0], toAtt[1], att.Position.Z);
                                    }
                                }
                            }

                            successCount++;
                        }
                        catch { /* 개별 에러 무시 */ }
                    }

                    newTrans.Commit();
                    AddLog($"총 {successCount}개의 객체 변환 성공.");
                }

                // 변환 완료 후 줌 적용 (Zoom Extents)
                CadApp.DocumentManager.MdiActiveDocument.SendStringToExecute("._ZOOM _E ", true, false, false);
                await ShowModernDialog("완료", "좌표계 변환이 성공적으로 완료되었습니다.");
            }
            catch (Exception ex)
            {
                await ShowModernDialog("오류", $"변환 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ShowModernDialog(string title, string content)
        {
            var msgBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = new System.Windows.Controls.TextBlock { Text = content, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10) },
                CloseButtonText = "확인",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 400,
                Height = 200
            };
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(msgBox);
            await msgBox.ShowDialogAsync();
        }
    }
}