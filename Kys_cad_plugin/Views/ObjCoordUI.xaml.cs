// 오토캐드 API
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
// 좌표 변환 API (ProjNet & GeoAPI)
using GeoAPI.CoordinateSystems.Transformations;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using System.Windows;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    public partial class ObjCoordUI : FluentWindow
    {
        // 1. 원본 코드의 좌표계 WKT 사전 (동일하게 유지)
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

        // 선택된 객체의 ID를 저장할 리스트
        private List<ObjectId> _selectedObjectIds = new List<ObjectId>();

        public ObjCoordUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
            InitUI();
        }

        private void InitUI()
        {
            var keys = WktDict.Keys.ToArray();
            CboSourceCS.ItemsSource = keys;
            CboTargetCS.ItemsSource = keys;

            CboSourceCS.SelectedIndex = 0; // 서해
            CboTargetCS.SelectedIndex = 1; // 서부
        }

        private void AddLog(string message, bool isError = false)
        {
            var tb = new System.Windows.Controls.TextBlock { Text = $"▶ {message}", FontSize = 11, Margin = new Thickness(2) };
            if (isError) tb.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
            else tb.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            LogListBox.Items.Add(tb);
            LogListBox.ScrollIntoView(tb);
        }

        // 1. 도면에서 객체 선택
        private void BtnSelectObjects_Click(object sender, RoutedEventArgs e)
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            this.WindowState = WindowState.Minimized; // 화면 숨기기

            try
            {
                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n좌표를 변환할 객체들을 선택하세요 (완료 시 Enter): ";

                PromptSelectionResult psr = ed.GetSelection(pso);

                if (psr.Status == PromptStatus.OK && psr.Value != null)
                {
                    _selectedObjectIds.Clear();
                    foreach (SelectedObject so in psr.Value)
                    {
                        _selectedObjectIds.Add(so.ObjectId);
                    }
                    TxtSelectedCount.Text = _selectedObjectIds.Count.ToString();
                    AddLog($"{_selectedObjectIds.Count}개의 객체가 선택되었습니다.");
                }
                else
                {
                    AddLog("객체 선택이 취소되었습니다.");
                }
            }
            catch (Exception ex)
            {
                AddLog($"선택 오류: {ex.Message}", true);
            }
            finally
            {
                this.WindowState = WindowState.Normal; // 창 복구
                this.Activate();
            }
        }

        // 2. 선택된 객체 좌표계 변환 (현재 도면에서 즉시 실행)
        private async void BtnTransform_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedObjectIds.Count == 0)
            {
                await ShowModernDialog("알림", "먼저 도면에서 변환할 객체를 선택해 주세요.");
                return;
            }

            string sourceKey = CboSourceCS.SelectedItem?.ToString();
            string targetKey = CboTargetCS.SelectedItem?.ToString();

            if (sourceKey == targetKey)
            {
                await ShowModernDialog("알림", "원본 좌표계와 대상 좌표계가 동일합니다.");
                return;
            }

            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                AddLog($"좌표계 변환을 시작합니다. ({sourceKey} -> {targetKey})");

                // ProjNet 변환기 세팅
                var csFactory = new CoordinateSystemFactory();
                var ctFactory = new CoordinateTransformationFactory();
                var sourceCS = csFactory.CreateFromWkt(WktDict[sourceKey]);
                var targetCS = csFactory.CreateFromWkt(WktDict[targetKey]);
                IMathTransform transform = ctFactory.CreateFromCoordinateSystems(sourceCS, targetCS).MathTransform;

                int successCount = 0;
                Extents3d totalExtents = new Extents3d();
                bool hasData = false;

                // [핵심] 현재 도면을 락(Lock) 걸고 트랜잭션 시작
                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (ObjectId objId in _selectedObjectIds)
                        {
                            if (objId.IsErased) continue;

                            // 선택된 객체를 쓰기(ForWrite) 모드로 염
                            Entity ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
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
                                        PolylineVertex3d vtx = tr.GetObject(vId, OpenMode.ForWrite) as PolylineVertex3d;
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

                                    // 블록 내부 속성 텍스트 위치도 같이 변환
                                    foreach (ObjectId attId in blockRef.AttributeCollection)
                                    {
                                        if (!attId.IsValid || attId.IsErased) continue;
                                        AttributeReference att = tr.GetObject(attId, OpenMode.ForWrite) as AttributeReference;
                                        if (att != null)
                                        {
                                            double[] toAtt = transform.Transform(new double[] { att.Position.X, att.Position.Y });
                                            att.Position = new Point3d(toAtt[0], toAtt[1], att.Position.Z);
                                        }
                                    }
                                }

                                // 줌 계산을 위해 BoundingBox 추출 (가능한 경우)
                                if (ent.Bounds.HasValue)
                                {
                                    if (!hasData)
                                    {
                                        totalExtents = ent.Bounds.Value;
                                        hasData = true;
                                    }
                                    else
                                    {
                                        totalExtents.AddExtents(ent.Bounds.Value);
                                    }
                                }

                                successCount++;
                            }
                            catch { /* 개별 객체 변환 에러 발생 시 무시하고 다음 진행 */ }
                        }

                        tr.Commit(); // 현재 도면에 변환된 값 저장
                    }
                }

                // 변환된 객체들을 화면에 꽉 차게 보여주는 줌(Zoom) 실행
                if (hasData)
                {
                    ed.UpdateScreen();
                    ViewTableRecord view = ed.GetCurrentView();
                    double width = totalExtents.MaxPoint.X - totalExtents.MinPoint.X;
                    double height = totalExtents.MaxPoint.Y - totalExtents.MinPoint.Y;

                    if (width == 0) width = 10;
                    if (height == 0) height = 10;

                    Point2d center = new Point2d((totalExtents.MaxPoint.X + totalExtents.MinPoint.X) / 2.0,
                                                 (totalExtents.MaxPoint.Y + totalExtents.MinPoint.Y) / 2.0);

                    view.CenterPoint = center;
                    view.Width = width * 1.5;
                    view.Height = height * 1.5;
                    ed.SetCurrentView(view);
                }

                _selectedObjectIds.Clear(); // 안전을 위해 선택 리스트 초기화
                TxtSelectedCount.Text = "0";

                AddLog($"총 {successCount}개의 객체가 성공적으로 변환되었습니다.");
                await ShowModernDialog("완료", "현재 도면에서 객체들의 좌표 변환이 완료되었습니다.");
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