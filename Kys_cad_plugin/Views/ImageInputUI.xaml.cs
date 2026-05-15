using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

// AutoCAD Namespaces
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

// WPF-UI & GIS Namespaces
using Wpf.Ui.Controls;
using DotSpatial.Projections;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    /// <summary>
    /// ImageInputUI.xaml에 대한 비즈니스 로직
    /// </summary>
    public partial class ImageInputUI : FluentWindow
    {
        private string _selectedImagePath = "";
        private int _pixelWidth = 1;
        private int _pixelHeight = 1;
        private double _pixelSizeX = 1;
        private double _pixelSizeY = -1;

        // 제공된 좌표계 WKT 사전
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

        public ImageInputUI()
        {
            InitializeComponent();
            InitializeComboBoxes();
            LoadCadLayers();
        }

        // 💡 ContentDialog 호출 헬퍼 메서드
        private async Task ShowSimpleDialog(string title, string content, ControlAppearance appearance = ControlAppearance.Info)
        {
            var dialog = new ContentDialog(RootDialogHost)
            {
                Title = title,
                Content = content,
                CloseButtonText = "확인",
                DefaultButton = ContentDialogButton.Close,
                PrimaryButtonAppearance = appearance
            };
            await dialog.ShowAsync();
        }

        private void InitializeComboBoxes()
        {
            CmbSourceCRS.Items.Add("알 수 없음 (변환 안함)");
            CmbTargetCRS.Items.Add("변환 안함");

            foreach (var key in WktDict.Keys)
            {
                CmbSourceCRS.Items.Add(key);
                CmbTargetCRS.Items.Add(key);
            }

            CmbSourceCRS.SelectedIndex = 0;
            CmbTargetCRS.SelectedIndex = 0;
        }

        private void LoadCadLayers()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in lt)
                {
                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    CmbLayers.Items.Add(ltr.Name);
                }
                CmbLayers.SelectedItem = ((LayerTableRecord)tr.GetObject(db.Clayer, OpenMode.ForRead)).Name;
            }
        }

        public async void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "이미지 파일|*.tif;*.tiff;*.jpg;*.jpeg;*.img|모든 파일|*.*" };
            if (ofd.ShowDialog() == true)
            {
                _selectedImagePath = ofd.FileName;
                TxtFilePath.Text = _selectedImagePath;

                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_selectedImagePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    ImgPreview.Source = bitmap;
                    _pixelWidth = bitmap.PixelWidth;
                    _pixelHeight = bitmap.PixelHeight;

                    ExtractGeodata();
                    CadCanvas.Children.Clear();
                }
                catch (Exception ex)
                {
                    await ShowSimpleDialog("이미지 오류", $"이미지를 로드할 수 없습니다: {ex.Message}", ControlAppearance.Danger);
                }
            }
        }

        private void ExtractGeodata()
        {
            string ext = Path.GetExtension(_selectedImagePath).ToLower();
            string worldFileExt = ext.StartsWith(".ti") ? ".tfw" : ext.StartsWith(".jp") ? ".jgw" : ".wld";
            string worldPath = Path.ChangeExtension(_selectedImagePath, worldFileExt);

            if (File.Exists(worldPath))
            {
                ReadWorldFile(worldPath);
                TxtSourceType.Text = $"World File ({worldFileExt.ToUpper()})";
                TxtSourceType.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
            else
            {
                TxtSourceType.Text = "좌표 없음 (기본값)";
                TxtSourceType.Foreground = System.Windows.Media.Brushes.OrangeRed;
                TxtInsertX.Text = "0"; TxtInsertY.Text = "0"; TxtScale.Text = "1";
            }
        }

        private void ReadWorldFile(string path)
        {
            try
            {
                string[] lines = File.ReadAllLines(path);
                if (lines.Length >= 6)
                {
                    _pixelSizeX = double.Parse(lines[0]);
                    _pixelSizeY = double.Parse(lines[3]);
                    double wX = double.Parse(lines[4]);
                    double wY = double.Parse(lines[5]);

                    TxtScale.Text = Math.Abs(_pixelSizeX).ToString();
                    TxtRotation.Text = lines[1];

                    // Map 3D 방식 보정: 월드파일(중심) -> 캐드(좌하단 모서리)
                    double realHeight = _pixelHeight * Math.Abs(_pixelSizeY);
                    double cX = wX - (_pixelSizeX / 2.0);
                    double cY = wY - realHeight + (Math.Abs(_pixelSizeY) / 2.0);

                    TxtInsertX.Text = cX.ToString("F3");
                    TxtInsertY.Text = cY.ToString("F3");
                }
            }
            catch { }
        }

        private void BtnRefreshPreview_Click(object sender, RoutedEventArgs e)
        {
            if (_pixelWidth <= 1) return;
            DrawCadPreview();
        }

        private void DrawCadPreview()
        {
            CadCanvas.Children.Clear();
            double.TryParse(TxtInsertX.Text, out double minX);
            double.TryParse(TxtInsertY.Text, out double minY);
            double.TryParse(TxtScale.Text, out double sc);
            if (sc <= 0) sc = 1;

            double maxX = minX + (_pixelWidth * sc);
            double maxY = minY + (_pixelHeight * sc);

            PreviewGrid.Width = _pixelWidth; PreviewGrid.Height = _pixelHeight;
            CadCanvas.Width = _pixelWidth; CadCanvas.Height = _pixelHeight;

            Document doc = Application.DocumentManager.MdiActiveDocument;
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(doc.Database), OpenMode.ForRead);
                int count = 0;
                foreach (ObjectId id in btr)
                {
                    if (count > 2000) break; // 성능 제한
                    if (tr.GetObject(id, OpenMode.ForRead) is Entity ent)
                    {
                        try
                        {
                            var ex = ent.GeometricExtents;
                            if (ex.MinPoint.X > maxX || ex.MaxPoint.X < minX || ex.MinPoint.Y > maxY || ex.MaxPoint.Y < minY) continue;

                            if (ent is Line line) { DrawLine(line.StartPoint, line.EndPoint, minX, maxY, sc); count++; }
                            else if (ent is Polyline poly)
                            {
                                for (int i = 0; i < poly.NumberOfVertices - 1; i++) { DrawLine(poly.GetPoint3dAt(i), poly.GetPoint3dAt(i + 1), minX, maxY, sc); count++; }
                            }
                        }
                        catch { }
                    }
                }
                tr.Commit();
            }
        }

        private void DrawLine(Point3d p1, Point3d p2, double minX, double maxY, double sc)
        {
            var l = new System.Windows.Shapes.Line
            {
                X1 = (p1.X - minX) / sc,
                Y1 = (maxY - p1.Y) / sc,
                X2 = (p2.X - minX) / sc,
                Y2 = (maxY - p2.Y) / sc,
                Stroke = System.Windows.Media.Brushes.Cyan,
                StrokeThickness = 1.5
            };
            CadCanvas.Children.Add(l);
        }

        public async void BtnInsert_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedImagePath)) { await ShowSimpleDialog("주의", "이미지를 선택해주세요.", ControlAppearance.Caution); return; }

            double.TryParse(TxtInsertX.Text, out double x);
            double.TryParse(TxtInsertY.Text, out double y);
            double.TryParse(TxtScale.Text, out double sc);
            double.TryParse(TxtRotation.Text, out double rot);
            string layer = CmbLayers.SelectedItem?.ToString() ?? "0";

            // 좌표 변환 로직
            if (CmbSourceCRS.SelectedIndex > 0 && CmbTargetCRS.SelectedIndex > 0)
            {
                try
                {
                    var pS = new ProjectionInfo(); pS.ParseEsriString(WktDict[CmbSourceCRS.SelectedItem.ToString()]);
                    var pT = new ProjectionInfo(); pT.ParseEsriString(WktDict[CmbTargetCRS.SelectedItem.ToString()]);
                    double[] xy = { x, y }; double[] z = { 0 };
                    Reproject.ReprojectPoints(xy, z, pS, pT, 0, 1);
                    x = xy[0]; y = xy[1];
                }
                catch (Exception ex) { await ShowSimpleDialog("변환 에러", ex.Message, ControlAppearance.Danger); return; }
            }

            InsertToAutoCAD(x, y, sc, rot, layer);
        }

        private async void InsertToAutoCAD(double x, double y, double sc, double rot, string layer)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor; // 💡 화면 제어를 위한 에디터 객체

            using (var lockDoc = doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    // ... [1. 이미지 정의 및 2. 객체 생성 로직은 이전과 동일] ...
                    var dictId = RasterImageDef.GetImageDictionary(doc.Database);
                    if (dictId.IsNull) dictId = RasterImageDef.CreateImageDictionary(doc.Database);
                    var dict = (DBDictionary)tr.GetObject(dictId, OpenMode.ForWrite);

                    string name = Path.GetFileNameWithoutExtension(_selectedImagePath);
                    RasterImageDef def; ObjectId defId;
                    if (dict.Contains(name)) { defId = dict.GetAt(name); def = (RasterImageDef)tr.GetObject(defId, OpenMode.ForWrite); }
                    else { def = new RasterImageDef { SourceFileName = _selectedImagePath }; def.Load(); defId = dict.SetAt(name, def); tr.AddNewlyCreatedDBObject(def, true); }

                    var img = new RasterImage { ImageDefId = defId, Layer = layer };
                    var uV = new Vector3d(_pixelWidth * sc, 0, 0).RotateBy(rot * (Math.PI / 180.0), Vector3d.ZAxis);
                    var vV = new Vector3d(0, _pixelHeight * sc, 0).RotateBy(rot * (Math.PI / 180.0), Vector3d.ZAxis);
                    img.Orientation = new CoordinateSystem3d(new Point3d(x, y, 0), uV, vV);

                    var btr = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(doc.Database), OpenMode.ForWrite);
                    ObjectId imgId = btr.AppendEntity(img);
                    tr.AddNewlyCreatedDBObject(img, true);
                    img.AssociateRasterDef(def);

                    // [3. 이미지를 맨 뒤로 보내기]
                    DrawOrderTable dot = (DrawOrderTable)tr.GetObject(btr.DrawOrderTableId, OpenMode.ForWrite);
                    ObjectIdCollection idCol = new ObjectIdCollection { imgId };
                    dot.MoveToBottom(idCol);

                    // 💡 4. 핵심: 삽입된 이미지 범위로 화면 이동 (Zoom to Image)
                    Extents3d imgExtents = img.GeometricExtents; // 이미지의 경계 좌표 추출

                    using (ViewTableRecord view = ed.GetCurrentView())
                    {
                        // 이미지 크기에 맞춰 뷰의 중심점과 높이 설정
                        view.CenterPoint = new Point2d((imgExtents.MinPoint.X + imgExtents.MaxPoint.X) / 2.0,
                                                       (imgExtents.MinPoint.Y + imgExtents.MaxPoint.Y) / 2.0);
                        view.Height = imgExtents.MaxPoint.Y - imgExtents.MinPoint.Y;
                        view.Width = imgExtents.MaxPoint.X - imgExtents.MinPoint.X;

                        ed.SetCurrentView(view); // 변경된 뷰 적용
                    }

                    tr.Commit();

                    // 💡 5. 화면 즉시 새로고침 (Regen)
                    ed.Regen();

                    this.Close(); // 작업 완료 후 창 닫기
                }
                catch (Exception ex)
                {
                    await ShowSimpleDialog("오류", ex.Message, ControlAppearance.Danger);
                }
            }
        }
    }
}