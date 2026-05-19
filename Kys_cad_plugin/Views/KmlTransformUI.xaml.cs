using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using System.Windows;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    public partial class KmlTransformUI : FluentWindow
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

        private double _lon0, _lat0, _fe, _fn, _k0;
        private bool _isLatLonDirect = false;

        public KmlTransformUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
            CmbCoordSystem.ItemsSource = WktDict.Keys.ToList();
            CmbCoordSystem.SelectedIndex = 2; // 중부원점 기본
        }

        private void AddExportLog(string msg) { ExportLogBox.Items.Add($"▶ {msg}"); ExportLogBox.ScrollIntoView(ExportLogBox.Items[ExportLogBox.Items.Count - 1]); }
        private void AddImportLog(string msg) { ImportLogBox.Items.Add($"▶ {msg}"); ImportLogBox.ScrollIntoView(ImportLogBox.Items[ImportLogBox.Items.Count - 1]); }

        private async System.Threading.Tasks.Task ShowModernDialog(string title, string content)
        {
            var msgBox = new Wpf.Ui.Controls.MessageBox { Title = title, Content = new System.Windows.Controls.TextBlock { Text = content, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10) }, CloseButtonText = "확인", Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(msgBox);
            await msgBox.ShowDialogAsync();
        }

        private void SetupProjectionParameters()
        {
            string selectedKey = CmbCoordSystem.SelectedItem as string ?? "중부";
            string wkt = WktDict[selectedKey];

            if (selectedKey == "경위도") { _isLatLonDirect = true; return; }

            _isLatLonDirect = false;
            _lon0 = ParseWktValue(wkt, "\"central_meridian\",", 127.0);
            _lat0 = ParseWktValue(wkt, "\"latitude_of_origin\",", 38.0);
            _fe = ParseWktValue(wkt, "\"false_easting\",", 200000.0);
            _fn = ParseWktValue(wkt, "\"false_northing\",", 600000.0);
            _k0 = ParseWktValue(wkt, "\"scale_factor\",", 1.0);
        }

        private double ParseWktValue(string wkt, string key, double defaultVal)
        {
            int idx = wkt.IndexOf(key);
            if (idx == -1) return defaultVal;
            int start = idx + key.Length;
            int end = wkt.IndexOf("]", start);
            if (end == -1) return defaultVal;
            string valStr = wkt.Substring(start, end - start).Trim();
            if (double.TryParse(valStr, out double val)) return val;
            return defaultVal;
        }

        // =========================================================================
        // ★ [NEW] 캐드 색상 <-> KML 색상(AABBGGRR) 양방향 변환 모듈
        // =========================================================================
        private byte[] GetEntityRgb(Entity ent, Transaction tr)
        {
            Autodesk.AutoCAD.Colors.Color color = ent.Color;
            // ByLayer일 경우 도면층의 실제 색상 추적
            if (color.IsByLayer)
            {
                LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                color = ltr.Color;
            }
            return new byte[] { color.ColorValue.R, color.ColorValue.G, color.ColorValue.B };
        }

        private string GetKmlColorString(byte[] rgb, string alphaHex = "ff")
        {
            // KML 컬러 형식: aabbggrr (투명도, 파랑, 초록, 빨강) 역순
            return $"{alphaHex}{rgb[2]:x2}{rgb[1]:x2}{rgb[0]:x2}";
        }

        private Autodesk.AutoCAD.Colors.Color GetCadColorFromKml(string kmlColor)
        {
            if (string.IsNullOrWhiteSpace(kmlColor)) return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 256);
            kmlColor = kmlColor.Trim().ToLower().Replace("#", "");

            try
            {
                if (kmlColor.Length == 8) // aabbggrr
                {
                    byte b = Convert.ToByte(kmlColor.Substring(2, 2), 16);
                    byte g = Convert.ToByte(kmlColor.Substring(4, 2), 16);
                    byte r = Convert.ToByte(kmlColor.Substring(6, 2), 16);
                    return Autodesk.AutoCAD.Colors.Color.FromRgb(r, g, b);
                }
                else if (kmlColor.Length == 6) // bbggrr
                {
                    byte b = Convert.ToByte(kmlColor.Substring(0, 2), 16);
                    byte g = Convert.ToByte(kmlColor.Substring(2, 2), 16);
                    byte r = Convert.ToByte(kmlColor.Substring(4, 2), 16);
                    return Autodesk.AutoCAD.Colors.Color.FromRgb(r, g, b);
                }
            }
            catch { }
            return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 256);
        }

        // =========================================================================
        // 내보내기 (Export) - 색상 적용
        // =========================================================================
        private async void BtnExportKml_Click(object sender, RoutedEventArgs e)
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;

            SaveFileDialog sfd = new SaveFileDialog { Filter = "구글어스 KML (*.kml)|*.kml|구글어스 압축 KMZ (*.kmz)|*.kmz", Title = "구글어스 파일로 저장" };
            if (sfd.ShowDialog() != true) return;

            SetupProjectionParameters();

            try
            {
                AddExportLog("도면 객체 스캔 및 색상/위경도 변환 시작...");
                XNamespace ns = "http://www.opengis.net/kml/2.2";
                var layerFolders = new Dictionary<string, XElement>();

                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                        int processedCount = 0;
                        foreach (ObjectId entId in btr)
                        {
                            Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;
                            string layerName = ent.Layer;

                            if (!layerFolders.ContainsKey(layerName))
                                layerFolders[layerName] = new XElement(ns + "Folder", new XElement(ns + "name", layerName));
                            XElement currentFolder = layerFolders[layerName];

                            // ★ 캐드 원본 색상 추출 (ByLayer 포함) 및 KML 스타일 노드 생성
                            byte[] rgb = GetEntityRgb(ent, tr);
                            string kmlLineColor = GetKmlColorString(rgb, "ff"); // 선은 불투명(ff)
                            string kmlPolyFill = GetKmlColorString(rgb, "4d");  // 면 채우기는 30% 반투명(4d)

                            XElement styleNode = new XElement(ns + "Style",
                                new XElement(ns + "LineStyle", new XElement(ns + "color", kmlLineColor), new XElement(ns + "width", "2")),
                                new XElement(ns + "PolyStyle", new XElement(ns + "color", kmlPolyFill)),
                                new XElement(ns + "LabelStyle", new XElement(ns + "color", kmlLineColor), new XElement(ns + "scale", "1.0"))
                            );

                            if (ent is Polyline pline)
                            {
                                var coordsList = new List<string>();
                                for (int i = 0; i < pline.NumberOfVertices; i++)
                                {
                                    Point2d pt = pline.GetPoint2dAt(i);
                                    ConvertTMtoGeo(pt.X, pt.Y, out double lat, out double lon);
                                    coordsList.Add($"{lon},{lat},0");
                                }
                                if (pline.Closed && pline.NumberOfVertices > 0)
                                {
                                    Point2d pt = pline.GetPoint2dAt(0);
                                    ConvertTMtoGeo(pt.X, pt.Y, out double lat, out double lon);
                                    coordsList.Add($"{lon},{lat},0");
                                }

                                XElement placemark = new XElement(ns + "Placemark",
                                    new XElement(ns + "name", $"Poly_{processedCount}"),
                                    styleNode, // 스타일 주입
                                    new XElement(ns + "LineString", new XElement(ns + "coordinates", string.Join(" ", coordsList)))
                                );

                                // 닫힌 폴리곤일 경우 Polygon 태그로 변경하여 면 색상까지 채움
                                if (pline.Closed)
                                {
                                    placemark.Element(ns + "LineString")?.ReplaceWith(
                                        new XElement(ns + "Polygon", new XElement(ns + "outerBoundaryIs", new XElement(ns + "LinearRing", new XElement(ns + "coordinates", string.Join(" ", coordsList)))))
                                    );
                                }
                                currentFolder.Add(placemark);
                                processedCount++;
                            }
                            else if (ent is DBPoint dbPt)
                            {
                                ConvertTMtoGeo(dbPt.Position.X, dbPt.Position.Y, out double lat, out double lon);
                                currentFolder.Add(new XElement(ns + "Placemark", new XElement(ns + "name", $"Pt_{processedCount}"), styleNode,
                                    new XElement(ns + "Point", new XElement(ns + "coordinates", $"{lon},{lat},0"))));
                                processedCount++;
                            }
                            else if (ent is DBText txt)
                            {
                                ConvertTMtoGeo(txt.Position.X, txt.Position.Y, out double lat, out double lon);
                                currentFolder.Add(new XElement(ns + "Placemark", new XElement(ns + "name", txt.TextString), styleNode,
                                    new XElement(ns + "Point", new XElement(ns + "coordinates", $"{lon},{lat},0"))));
                                processedCount++;
                            }
                        }
                        tr.Commit();
                    }
                }

                XElement documentNode = new XElement(ns + "Document", new XElement(ns + "name", Path.GetFileName(sfd.FileName)));
                foreach (var folder in layerFolders.Values)
                {
                    if (folder.Elements(ns + "Placemark").Any()) documentNode.Add(folder);
                }

                XDocument kmlDoc = new XDocument(new XElement(ns + "kml", documentNode));

                if (sfd.FileName.EndsWith(".kmz", StringComparison.OrdinalIgnoreCase))
                {
                    string tempKml = Path.Combine(Path.GetTempPath(), "doc.kml");
                    kmlDoc.Save(tempKml);
                    if (File.Exists(sfd.FileName)) File.Delete(sfd.FileName);
                    using (ZipArchive archive = ZipFile.Open(sfd.FileName, ZipArchiveMode.Create)) { archive.CreateEntryFromFile(tempKml, "doc.kml"); }
                    File.Delete(tempKml);
                    AddExportLog($"도면 보존 KMZ(압축) 저장 성공!");
                }
                else
                {
                    kmlDoc.Save(sfd.FileName);
                    AddExportLog($"도면 KML 파일 출력 완료.");
                }

                await ShowModernDialog("성공", "내보내기에 성공했습니다!");
            }
            catch (Exception ex) { AddExportLog($"에러: {ex.Message}"); }
        }

        // =========================================================================
        // 가져오기 (Import) - 색상 복원
        // =========================================================================
        private void BtnSelectImportFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "구글어스 파일 (*.kml;*.kmz)|*.kml;*.kmz" };
            if (ofd.ShowDialog() == true) { TxtImportPath.Text = ofd.FileName; AddImportLog($"로드: {Path.GetFileName(ofd.FileName)}"); }
        }

        private async void BtnImportKml_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtImportPath.Text) || !File.Exists(TxtImportPath.Text)) return;
            SetupProjectionParameters();

            string targetLayerName = TxtImportLayer.Text.Trim();
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;

            try
            {
                AddImportLog("구글어스 XML 파싱 및 색상 복원 시작...");
                XDocument kmlDoc = null;

                if (TxtImportPath.Text.EndsWith(".kmz", StringComparison.OrdinalIgnoreCase))
                {
                    using (ZipArchive archive = ZipFile.OpenRead(TxtImportPath.Text))
                    {
                        var kmlEntry = archive.Entries.FirstOrDefault(entry => entry.Name.EndsWith(".kml", StringComparison.OrdinalIgnoreCase));
                        using (var stream = kmlEntry.Open()) { kmlDoc = XDocument.Load(stream); }
                    }
                }
                else { kmlDoc = XDocument.Load(TxtImportPath.Text); }

                XNamespace ns = "http://www.opengis.net/kml/2.2";
                var placemarks = kmlDoc.Descendants(ns + "Placemark").ToList();
                int importedCount = 0;

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
                            LayerTableRecord ltr = new LayerTableRecord { Name = targetLayerName, Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 256) };
                            layerId = lt.Add(ltr); tr.AddNewlyCreatedDBObject(ltr, true);
                        }

                        foreach (var pm in placemarks)
                        {
                            // ★ KML 색상(Style) 파싱 및 캐드 색상으로 역추적
                            Autodesk.AutoCAD.Colors.Color cadColor = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 256); // 기본 ByLayer

                            // 인라인 스타일이 있는지 확인
                            XElement styleNode = pm.Element(ns + "Style");

                            // 인라인이 없으면 문서 내 공용 스타일(styleUrl) 참조
                            if (styleNode == null)
                            {
                                string styleUrl = pm.Element(ns + "styleUrl")?.Value?.Trim('#');
                                if (!string.IsNullOrEmpty(styleUrl))
                                {
                                    styleNode = kmlDoc.Descendants(ns + "Style").FirstOrDefault(s => s.Attribute("id")?.Value == styleUrl);
                                }
                            }

                            if (styleNode != null)
                            {
                                // LineStyle이나 PolyStyle 안의 <color> 값 검색
                                string colorStr = styleNode.Descendants(ns + "color").FirstOrDefault()?.Value;
                                if (!string.IsNullOrEmpty(colorStr)) cadColor = GetCadColorFromKml(colorStr);
                            }

                            // 1. 라인, 폴리곤 처리
                            var coordsNode = pm.Descendants(ns + "coordinates").FirstOrDefault(n => n.Parent.Name.LocalName == "LineString" || n.Parent.Name.LocalName == "LinearRing");
                            if (coordsNode != null)
                            {
                                string coordsText = coordsNode.Value?.Trim() ?? "";
                                var tokens = coordsText.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                Polyline pline = new Polyline { LayerId = layerId, Color = cadColor }; // ★ 색상 주입

                                int vIdx = 0;
                                foreach (var token in tokens)
                                {
                                    var xyz = token.Split(',');
                                    if (xyz.Length >= 2 && double.TryParse(xyz[0], out double lon) && double.TryParse(xyz[1], out double lat))
                                    {
                                        ConvertGeoToTM(lat, lon, out double cadX, out double cadY);
                                        pline.AddVertexAt(vIdx, new Point2d(cadX, cadY), 0, 0, 0);
                                        vIdx++;
                                    }
                                }
                                if (vIdx >= 2) { btr.AppendEntity(pline); tr.AddNewlyCreatedDBObject(pline, true); importedCount++; }
                            }

                            // 2. 포인트 처리
                            var pointNode = pm.Element(ns + "Point") ?? pm.Descendants(ns + "Point").FirstOrDefault();
                            if (pointNode != null)
                            {
                                string coordText = pointNode.Element(ns + "coordinates")?.Value?.Trim() ?? "";
                                var xyz = coordText.Split(',');
                                if (xyz.Length >= 2 && double.TryParse(xyz[0], out double lon) && double.TryParse(xyz[1], out double lat))
                                {
                                    ConvertGeoToTM(lat, lon, out double cadX, out double cadY);
                                    DBPoint dbPt = new DBPoint(new Point3d(cadX, cadY, 0)) { LayerId = layerId, Color = cadColor }; // ★ 색상 주입
                                    btr.AppendEntity(dbPt); tr.AddNewlyCreatedDBObject(dbPt, true);
                                    importedCount++;
                                }
                            }
                        }
                        tr.Commit();
                    }
                }
                doc.Editor.UpdateScreen();
                AddImportLog($"가져오기 완료! 총 {importedCount}개의 객체가 색상을 유지한 채 복원되었습니다.");
                await ShowModernDialog("완료", "구글어스 데이터를 도면에 성공적으로 불러왔습니다!");
            }
            catch (Exception ex) { AddImportLog($"에러: {ex.Message}"); }
        }

        // =========================================================================
        // 투영 수학 공식 영역 생략... (이전 답변과 동일하게 유지)
        // =========================================================================
        private void ConvertTMtoGeo(double x, double y, out double lat, out double lon)
        {
            if (_isLatLonDirect) { lat = y; lon = x; return; }
            double lonOrigin = _lon0 * Math.PI / 180.0; double latOrigin = _lat0 * Math.PI / 180.0;
            double a = 6378137.0; double e2 = 0.00669437999014; double e4 = e2 * e2; double e6 = e4 * e2; double ep2 = e2 / (1.0 - e2);
            double M0 = a * ((1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * latOrigin - (3 * e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * Math.Sin(2 * latOrigin) + (15 * e4 / 256 + 45 * e6 / 1024) * Math.Sin(4 * latOrigin) - (35 * e6 / 3072) * Math.Sin(6 * latOrigin));
            double M = M0 + (y - _fn) / _k0; double mu = M / (a * (1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256));
            double e1 = (1 - Math.Sqrt(1 - e2)) / (1 + Math.Sqrt(1 - e2));
            double lat1 = mu + (3 * e1 / 2 - 27 * e1 * e1 * e1 / 32) * Math.Sin(2 * mu) + (21 * e1 * e1 / 16 - 55 * e1 * e1 * e1 * e1 / 32) * Math.Sin(4 * mu) + (151 * e1 * e1 * e1 / 96) * Math.Sin(6 * mu);
            double N1 = a / Math.Sqrt(1 - e2 * Math.Sin(lat1) * Math.Sin(lat1)); double T1 = Math.Tan(lat1) * Math.Tan(lat1); double C1 = ep2 * Math.Cos(lat1) * Math.Cos(lat1); double R1 = a * (1 - e2) / Math.Pow(1 - e2 * Math.Sin(lat1) * Math.Sin(lat1), 1.5); double D = (x - _fe) / (N1 * _k0);
            double latRad = lat1 - (N1 * Math.Tan(lat1) / R1) * (D * D / 2 - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * ep2) * D * D * D * D / 24 + (61 + 90 * T1 + 298 * C1 + 45 * T1 * T1 - 252 * ep2 - 3 * C1 * C1) * D * D * D * D * D * D / 720);
            double lonRad = lonOrigin + (D - (1 + 2 * T1 + C1) * D * D * D / 6 + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * ep2 + 24 * T1 * T1) * D * D * D * D * D / 120) / Math.Cos(lat1);
            lat = latRad * 180.0 / Math.PI; lon = lonRad * 180.0 / Math.PI;
        }

        private void ConvertGeoToTM(double lat, double lon, out double x, out double y)
        {
            if (_isLatLonDirect) { x = lon; y = lat; return; }
            double latRad = lat * Math.PI / 180.0; double lonRad = lon * Math.PI / 180.0; double lonOrigin = _lon0 * Math.PI / 180.0; double latOrigin = _lat0 * Math.PI / 180.0;
            double a = 6378137.0; double e2 = 0.00669437999014; double e4 = e2 * e2; double e6 = e4 * e2; double ep2 = e2 / (1.0 - e2);
            double N = a / Math.Sqrt(1 - e2 * Math.Sin(latRad) * Math.Sin(latRad)); double T = Math.Tan(latRad) * Math.Tan(latRad); double C = ep2 * Math.Cos(latRad) * Math.Cos(latRad); double A = (lonRad - lonOrigin) * Math.Cos(latRad);
            double M = a * ((1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * latRad - (3 * e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * Math.Sin(2 * latRad) + (15 * e4 / 256 + 45 * e6 / 1024) * Math.Sin(4 * latRad) - (35 * e6 / 3072) * Math.Sin(6 * latRad));
            double M0 = a * ((1 - e2 / 4 - 3 * e4 / 64 - 5 * e6 / 256) * latOrigin - (3 * e2 / 8 + 3 * e4 / 32 + 45 * e6 / 1024) * Math.Sin(2 * latOrigin) + (15 * e4 / 256 + 45 * e6 / 1024) * Math.Sin(4 * latOrigin) - (35 * e6 / 3072) * Math.Sin(6 * latOrigin));
            x = _fe + _k0 * N * (A + (1 - T + C) * A * A * A / 6 + (5 - 18 * T + T * T + 72 * C - 58 * ep2) * A * A * A * A * A / 120);
            y = _fn + _k0 * (M - M0 + N * Math.Tan(latRad) * (A * A / 2 + (5 - T + 9 * C + 4 * C * C) * A * A * A * A / 24 + (61 - 58 * T + T * T + 600 * C - 330 * ep2) * A * A * A * A * A * A / 720));
        }
    }
}