using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Microsoft.Win32;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using NtsPoint = NetTopologySuite.Geometries.Point;

namespace Kys_cad_plugin.Views
{
    public partial class ShpImportUI : FluentWindow
    {
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

        private struct ProjParams
        {
            public bool IsLatLon;
            public double Lon0, Lat0, Fe, Fn, K0;
        }

        private ProjParams _dwgProj;
        private ProjParams _shpProj;
        private bool _skipProjection = false; // DWG와 SHP 좌표계가 완벽히 같으면 수학적 오차 방지를 위해 투영 생략

        public ShpImportUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

            var coordList = WktDict.Keys.ToList();
            CmbDwgCoord.ItemsSource = coordList;
            CmbShpCoord.ItemsSource = coordList;
            CmbDwgCoord.SelectedIndex = 2; // 중부
            CmbShpCoord.SelectedIndex = 2; // 중부
        }

        private void AddLog(string message, bool isError = false)
        {
            var tb = new System.Windows.Controls.TextBlock { Text = $"▶ {message}", FontSize = 11, Margin = new Thickness(2) };
            if (isError) tb.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
            else tb.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            LogListBox.Items.Add(tb);
            LogListBox.ScrollIntoView(tb);
        }

        private async System.Threading.Tasks.Task ShowModernDialog(string title, string content)
        {
            var msgBox = new Wpf.Ui.Controls.MessageBox { Title = title, Content = new System.Windows.Controls.TextBlock { Text = content, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10) }, CloseButtonText = "확인", Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(msgBox);
            await msgBox.ShowDialogAsync();
        }

        // =========================================================================================
        // ★ 1. PRJ 자동 감지 및 콤보박스 매칭 시스템
        // =========================================================================================
        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "Shapefile (*.shp)|*.shp", Title = "불러올 SHP 파일 선택" };
            if (ofd.ShowDialog() == true)
            {
                TxtFilePath.Text = ofd.FileName;
                AddLog($"파일 선택됨: {Path.GetFileName(ofd.FileName)}");

                // PRJ 파일 자동 탐색
                string prjPath = Path.ChangeExtension(ofd.FileName, ".prj");
                if (File.Exists(prjPath))
                {
                    try
                    {
                        string prjText = File.ReadAllText(prjPath);
                        string matchedKey = MatchPrjToDropdown(prjText.ToLower());

                        if (!string.IsNullOrEmpty(matchedKey))
                        {
                            CmbShpCoord.SelectedItem = matchedKey;
                            AddLog($"PRJ 파일 감지 성공! 원본 SHP 좌표계를 [{matchedKey}]로 자동 설정했습니다.");
                        }
                        else
                        {
                            AddLog($"PRJ 파일을 찾았으나 내부 파라미터가 등록된 목록과 일치하지 않습니다. 수동으로 원본 좌표계를 선택해주세요.", true);
                        }
                    }
                    catch { AddLog("PRJ 파일을 읽는 중 오류가 발생했습니다.", true); }
                }
                else
                {
                    AddLog($"PRJ 파일이 존재하지 않습니다. 원본 SHP 좌표계를 수동으로 선택해주세요.");
                }
            }
        }

        private string MatchPrjToDropdown(string prjText)
        {
            if (prjText.Contains("wgs_1984") && !prjText.Contains("transverse_mercator")) return "경위도";

            double pLon = ParseWktValue(prjText, "central_meridian", -999);
            double pFe = ParseWktValue(prjText, "false_easting", -999);
            double pFn = ParseWktValue(prjText, "false_northing", -999);
            double pK0 = ParseWktValue(prjText, "scale_factor", 1.0);

            foreach (var kvp in WktDict)
            {
                if (kvp.Key == "경위도") continue;
                string wktLower = kvp.Value.ToLower();
                double dLon = ParseWktValue(wktLower, "\"central_meridian\",", -999);
                double dFe = ParseWktValue(wktLower, "\"false_easting\",", -999);
                double dFn = ParseWktValue(wktLower, "\"false_northing\",", -999);
                double dK0 = ParseWktValue(wktLower, "\"scale_factor\",", 1.0);

                // 오차 범위를 고려한 정밀 비교 (Double Float 매칭)
                if (Math.Abs(pLon - dLon) < 0.05 && Math.Abs(pFe - dFe) < 1.0 && Math.Abs(pFn - dFn) < 1.0 && Math.Abs(pK0 - dK0) < 0.001)
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        private double ParseWktValue(string wkt, string key, double defaultVal)
        {
            int idx = wkt.IndexOf(key);
            if (idx == -1) return defaultVal;
            int start = wkt.IndexOf(",", idx) + 1;
            int end = wkt.IndexOf("]", start);
            if (end == -1) return defaultVal;
            string valStr = wkt.Substring(start, end - start).Trim();
            if (double.TryParse(valStr, out double val)) return val;
            return defaultVal;
        }

        private ProjParams SetupProjParams(string key)
        {
            ProjParams p = new ProjParams();
            string wkt = WktDict[key].ToLower();
            if (key == "경위도") { p.IsLatLon = true; return p; }
            p.IsLatLon = false;
            p.Lon0 = ParseWktValue(wkt, "\"central_meridian\",", 127.0);
            p.Lat0 = ParseWktValue(wkt, "\"latitude_of_origin\",", 38.0);
            p.Fe = ParseWktValue(wkt, "\"false_easting\",", 200000.0);
            p.Fn = ParseWktValue(wkt, "\"false_northing\",", 600000.0);
            p.K0 = ParseWktValue(wkt, "\"scale_factor\",", 1.0);
            return p;
        }

        // =========================================================================================
        // ★ 2. 작도 및 투영 연산 코어
        // =========================================================================================
        private async void BtnImportShp_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtFilePath.Text) || !File.Exists(TxtFilePath.Text))
            {
                await ShowModernDialog("알림", "먼저 불러올 SHP 파일을 선택해주세요.");
                return;
            }

            string dwgKey = CmbDwgCoord.SelectedItem?.ToString() ?? "중부";
            string shpKey = CmbShpCoord.SelectedItem?.ToString() ?? "중부";

            _dwgProj = SetupProjParams(dwgKey);
            _shpProj = SetupProjParams(shpKey);
            _skipProjection = (dwgKey == shpKey); // 좌표계가 완전히 같으면 수학적 오차 방지를 위해 투영 생략!

            string targetLayerName = string.IsNullOrWhiteSpace(TxtLayerName.Text) ? "SHP_IMPORT_DATA" : TxtLayerName.Text.Trim();
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;

            int successCount = 0;

            try
            {
                if (_skipProjection) AddLog($"DWG와 SHP의 좌표계가 [{dwgKey}]로 동일하여 변환 없이 원본 그대로 작도합니다.");
                else AddLog($"[{shpKey}] -> [{dwgKey}] 정밀 투영 변환 작도를 시작합니다...");

                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                        LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                        ObjectId layerId;
                        if (lt.Has(targetLayerName)) layerId = lt[targetLayerName];
                        else
                        {
                            lt.UpgradeOpen();
                            LayerTableRecord ltr = new LayerTableRecord { Name = targetLayerName, Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3) };
                            layerId = lt.Add(ltr);
                            tr.AddNewlyCreatedDBObject(ltr, true);
                        }

                        using (var reader = new ShapefileDataReader(TxtFilePath.Text, new GeometryFactory()))
                        {
                            while (reader.Read())
                            {
                                var geom = reader.Geometry;
                                if (geom == null) continue;
                                DrawGeometryToCad(geom, btr, tr, layerId);
                                successCount++;
                            }
                        }
                        tr.Commit();
                    }
                }
                doc.Editor.UpdateScreen();
                AddLog($"작도 완료! 총 {successCount}개의 객체가 변환되어 도면에 생성되었습니다.");
                await ShowModernDialog("성공", $"SHP 데이터 {successCount}개를 성공적으로 도면에 그렸습니다!");
            }
            catch (Exception ex)
            {
                AddLog($"오류 발생: {ex.Message}", true);
                await ShowModernDialog("오류", $"SHP 불러오기 중 오류가 발생했습니다:\n{ex.Message}");
            }
        }

        private void DrawGeometryToCad(Geometry geom, BlockTableRecord btr, Transaction tr, ObjectId layerId)
        {
            if (geom is Polygon polygon)
            {
                DrawLinearRing(polygon.ExteriorRing, btr, tr, layerId, true);
                foreach (var hole in polygon.InteriorRings) DrawLinearRing(hole, btr, tr, layerId, true);
            }
            else if (geom is MultiPolygon multiPolygon) { foreach (var p in multiPolygon.Geometries) DrawGeometryToCad(p, btr, tr, layerId); }
            else if (geom is LineString lineString) { DrawLinearRing(lineString, btr, tr, layerId, false); }
            else if (geom is MultiLineString multiLineString) { foreach (var ls in multiLineString.Geometries) DrawGeometryToCad(ls, btr, tr, layerId); }
            else if (geom is NtsPoint pt)
            {
                ApplyProjection(pt.X, pt.Y, out double dwgX, out double dwgY);
                DBPoint dbp = new DBPoint(new Point3d(dwgX, dwgY, 0)) { LayerId = layerId };
                btr.AppendEntity(dbp); tr.AddNewlyCreatedDBObject(dbp, true);
            }
            else if (geom is MultiPoint multiPoint) { foreach (var p in multiPoint.Geometries) DrawGeometryToCad(p, btr, tr, layerId); }
        }

        private void DrawLinearRing(LineString ls, BlockTableRecord btr, Transaction tr, ObjectId layerId, bool isClosed)
        {
            if (ls == null || ls.Coordinates.Length < 2) return;
            Polyline pline = new Polyline { LayerId = layerId };

            int vertexIndex = 0;
            for (int i = 0; i < ls.Coordinates.Length; i++)
            {
                var coord = ls.Coordinates[i];
                if (isClosed && i == ls.Coordinates.Length - 1 && coord.Equals2D(ls.Coordinates[0])) continue;

                // ★ NTS X, Y 좌표를 낚아채서 DWG 좌표로 투영 변환
                ApplyProjection(coord.X, coord.Y, out double dwgX, out double dwgY);

                pline.AddVertexAt(vertexIndex, new Point2d(dwgX, dwgY), 0, 0, 0);
                vertexIndex++;
            }
            pline.Closed = isClosed;
            btr.AppendEntity(pline); tr.AddNewlyCreatedDBObject(pline, true);
        }

        // =========================================================================================
        // ★ 3. SHP -> WGS84 -> DWG 순차 투영 파이프라인
        // =========================================================================================
        private void ApplyProjection(double shpX, double shpY, out double dwgX, out double dwgY)
        {
            // 좌표계가 같으면 불필요한 수학적 부동소수점 오차를 내지 않고 원본을 그대로 리턴합니다.
            if (_skipProjection)
            {
                dwgX = shpX; dwgY = shpY;
                return;
            }

            double lat, lon;

            // 1단계: SHP 좌표 -> WGS84 위경도 변환
            if (_shpProj.IsLatLon) { lat = shpY; lon = shpX; }
            else { ConvertTMtoGeo(shpX, shpY, _shpProj, out lat, out lon); }

            // 2단계: WGS84 위경도 -> 현재 DWG 투영 좌표 변환
            if (_dwgProj.IsLatLon) { dwgX = lon; dwgY = lat; }
            else { ConvertGeoToTM(lat, lon, _dwgProj, out dwgX, out dwgY); }
        }

        private void ConvertTMtoGeo(double x, double y, ProjParams p, out double lat, out double lon)
        {
            double lonOrigin = p.Lon0 * Math.PI / 180.0; double latOrigin = p.Lat0 * Math.PI / 180.0;
            double a = 6378137.0; double e2 = 0.00669437999014; double e4 = e2 * e2; double e6 = e4 * e2; double ep2 = e2 / (1.0 - e2);
            double M0 = a * ((1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * latOrigin - (3 * e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * Math.Sin(2 * latOrigin) + (15 * e4 / 256 + 45 * e6 / 1024) * Math.Sin(4 * latOrigin) - (35 * e6 / 3072) * Math.Sin(6 * latOrigin));
            double M = M0 + (y - p.Fn) / p.K0; double mu = M / (a * (1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256));
            double e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));
            double lat1 = mu + (3 * e1 / 2 - 27 * e1 * e1 * e1 / 32) * Math.Sin(2 * mu) + (21 * e1 * e1 / 16 - 55 * e1 * e1 * e1 * e1 / 32) * Math.Sin(4 * mu) + (151 * e1 * e1 * e1 / 96) * Math.Sin(6 * mu);
            double N1 = a / Math.Sqrt(1 - e2 * Math.Sin(lat1) * Math.Sin(lat1)); double T1 = Math.Tan(lat1) * Math.Tan(lat1); double C1 = ep2 * Math.Cos(lat1) * Math.Cos(lat1); double R1 = a * (1 - e2) / Math.Pow(1 - e2 * Math.Sin(lat1) * Math.Sin(lat1), 1.5); double D = (x - p.Fe) / (N1 * p.K0);
            double latRad = lat1 - (N1 * Math.Tan(lat1) / R1) * (D * D / 2 - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * ep2) * D * D * D * D / 24 + (61 + 90 * T1 + 298 * C1 + 45 * T1 * T1 - 252 * ep2 - 3 * C1 * C1) * D * D * D * D * D * D / 720);
            double lonRad = lonOrigin + (D - (1 + 2 * T1 + C1) * D * D * D / 6 + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * ep2 + 24 * T1 * T1) * D * D * D * D * D / 120) / Math.Cos(lat1);
            lat = latRad * 180.0 / Math.PI; lon = lonRad * 180.0 / Math.PI;
        }

        private void ConvertGeoToTM(double lat, double lon, ProjParams p, out double x, out double y)
        {
            double latRad = lat * Math.PI / 180.0; double lonRad = lon * Math.PI / 180.0; double lonOrigin = p.Lon0 * Math.PI / 180.0; double latOrigin = p.Lat0 * Math.PI / 180.0;
            double a = 6378137.0; double e2 = 0.00669437999014; double e4 = e2 * e2; double e6 = e4 * e2; double ep2 = e2 / (1.0 - e2);
            double N = a / Math.Sqrt(1 - e2 * Math.Sin(latRad) * Math.Sin(latRad)); double T = Math.Tan(latRad) * Math.Tan(latRad); double C = ep2 * Math.Cos(latRad) * Math.Cos(latRad); double A = (lonRad - lonOrigin) * Math.Cos(latRad);
            double M = a * ((1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * latRad - (3 * e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * Math.Sin(2 * latRad) + (15 * e4 / 256 + 45 * e6 / 1024) * Math.Sin(4 * latRad) - (35 * e6 / 3072) * Math.Sin(6 * latRad));
            double M0 = a * ((1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * latOrigin - (3 * e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * Math.Sin(2 * latOrigin) + (15 * e4 / 256 + 45 * e6 / 1024) * Math.Sin(4 * latOrigin) - (35 * e6 / 3072) * Math.Sin(6 * latOrigin));
            x = p.Fe + p.K0 * N * (A + (1 - T + C) * A * A * A / 6 + (5 - 18 * T + T * T + 72 * C - 58 * ep2) * A * A * A * A * A / 120);
            y = p.Fn + p.K0 * (M - M0 + N * Math.Tan(latRad) * (A * A / 2 + (5 - T + 9 * C + 4 * C * C) * A * A * A * A / 24 + (61 - 58 * T + T * T + 600 * C - 330 * ep2) * A * A * A * A * A * A / 720));
        }
    }
}