using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
// ★ DataImportManager가 있는 Core 네임스페이스 추가
using Kys_cad_plugin.Core;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    public partial class EoIdChangeUI : FluentWindow
    {
        private List<EoData> _allEoData = new List<EoData>();
        public ObservableCollection<CourseGroup> CourseGroups { get; set; } = new ObservableCollection<CourseGroup>();

        public EoIdChangeUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
            CourseDataGrid.ItemsSource = CourseGroups;
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

        // ★ 1. 파일 찾기 및 DataImportManager 연동 (Column Mapping)
        private async void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            // EO 데이터 분석에 필요한 필수 필드 목록 정의
            var requiredFields = new List<string> { "ID", "GPSTime", "X", "Y", "Z", "Omega", "Phi", "Kappa" };

            // 중앙 매핑 다이얼로그 호출
            var importResult = await DataImportManager.ImportAndMap(this, requiredFields);

            if (importResult != null && importResult.SuccessCount > 0)
            {
                TxtFilePath.Text = importResult.FileName;
                _allEoData.Clear();

                // 매핑된 결과를 EoData 모델 객체로 변환하여 저장
                foreach (var row in importResult.Rows)
                {
                    if (double.TryParse(row["GPSTime"], out double gpsTime))
                    {
                        double.TryParse(row["X"], out double nx);
                        double.TryParse(row["Y"], out double ny);

                        _allEoData.Add(new EoData
                        {
                            OriginalId = row["ID"],
                            GpsTime = gpsTime,
                            X = row["X"],
                            Y = row["Y"],
                            Z = row["Z"],
                            Omg = row["Omega"],
                            Phi = row["Phi"],
                            Kap = row["Kappa"],
                            NumX = nx,
                            NumY = ny
                        });
                    }
                }

                await ShowModernDialog("불러오기 완료", $"총 {_allEoData.Count}개의 데이터를 성공적으로 매핑했습니다.\n설정을 확인하고 '코스 분석 및 작도' 버튼을 눌러주세요.");
            }
        }

        // ★ 2. 코스 분석 및 도면에 출력
        private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (_allEoData.Count == 0)
            {
                await ShowModernDialog("알림", "먼저 '파일 찾기'를 통해 EO 데이터를 불러오고 매핑해주세요.");
                return;
            }

            if (!double.TryParse(TxtTimeGap.Text, out double timeGapThreshold))
            {
                await ShowModernDialog("오류", "시간차(초)는 숫자로 입력해주세요.");
                return;
            }

            try
            {
                CourseGroups.Clear();

                List<CourseGroup> tempGroups = new List<CourseGroup>();
                int courseIndex = 1;
                CourseGroup currentGroup = new CourseGroup { CourseIndex = courseIndex, NewCourseName = courseIndex.ToString() };
                currentGroup.EoItems.Add(_allEoData[0]);

                // 시간차를 기준으로 코스 분할
                for (int i = 1; i < _allEoData.Count; i++)
                {
                    if (Math.Abs(_allEoData[i].GpsTime - _allEoData[i - 1].GpsTime) > timeGapThreshold)
                    {
                        tempGroups.Add(currentGroup);
                        courseIndex++;
                        currentGroup = new CourseGroup { CourseIndex = courseIndex, NewCourseName = courseIndex.ToString() };
                    }
                    currentGroup.EoItems.Add(_allEoData[i]);
                }
                tempGroups.Add(currentGroup);

                // 좌측부터 우측으로 오름차순 정렬
                foreach (var group in tempGroups)
                {
                    group.EoItems.Sort((a, b) => a.NumX.CompareTo(b.NumX));
                    group.OldStartId = group.EoItems[0].OriginalId;
                    CourseGroups.Add(group);
                }

                // 캐드 화면에 작도 (원, 박스, 텍스트)
                DrawEoDataToCad();

                await ShowModernDialog("분석 완료", $"총 {CourseGroups.Count}개의 코스를 자동 분류하고 도면에 출력했습니다.");
            }
            catch (Exception ex)
            {
                await ShowModernDialog("오류", $"분석 중 오류: {ex.Message}");
            }
        }

        // ★ 3. 캐드 객체 생성 로직
        private void DrawEoDataToCad()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;

            if (!double.TryParse(TxtTextSize.Text, out double textHeight))
            {
                textHeight = 10.0;
            }

            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    ObjectId textLayer = GetOrCreateLayer(db, tr, "KYSQL_EO_TEXT", 2);
                    ObjectId boxLayer = GetOrCreateLayer(db, tr, "KYSQL_EO_BOX", 3);
                    ObjectId markerLayer = GetOrCreateLayer(db, tr, "KYSQL_EO_MARKER", 1);

                    foreach (var group in CourseGroups)
                    {
                        if (group.EoItems.Count == 0) continue;

                        double minX = double.MaxValue, minY = double.MaxValue;
                        double maxX = double.MinValue, maxY = double.MinValue;

                        foreach (var eo in group.EoItems)
                        {
                            DBText txt = new DBText();
                            txt.Position = new Point3d(eo.NumX, eo.NumY, 0);
                            txt.TextString = eo.OriginalId;
                            txt.Height = textHeight;
                            txt.LayerId = textLayer;
                            btr.AppendEntity(txt);
                            tr.AddNewlyCreatedDBObject(txt, true);

                            if (eo.NumX < minX) minX = eo.NumX;
                            if (eo.NumY < minY) minY = eo.NumY;
                            if (eo.NumX > maxX) maxX = eo.NumX;
                            if (eo.NumY > maxY) maxY = eo.NumY;
                        }

                        group.MinX = minX; group.MinY = minY; group.MaxX = maxX; group.MaxY = maxY;

                        // 시작점(제일 왼쪽) 강조 마커 (동심원 200, 300)
                        var startEo = group.EoItems[0];
                        Point3d centerPt = new Point3d(startEo.NumX, startEo.NumY, 0);

                        Circle circle200 = new Circle { Center = centerPt, Radius = 200.0, LayerId = markerLayer };
                        btr.AppendEntity(circle200);
                        tr.AddNewlyCreatedDBObject(circle200, true);

                        Circle circle300 = new Circle { Center = centerPt, Radius = 300.0, LayerId = markerLayer };
                        btr.AppendEntity(circle300);
                        tr.AddNewlyCreatedDBObject(circle300, true);

                        // 바운딩 박스
                        Polyline box = new Polyline();
                        double pad = 50.0;
                        box.AddVertexAt(0, new Point2d(minX - pad, minY - pad), 0, 0, 0);
                        box.AddVertexAt(1, new Point2d(maxX + pad, minY - pad), 0, 0, 0);
                        box.AddVertexAt(2, new Point2d(maxX + pad, maxY + pad), 0, 0, 0);
                        box.AddVertexAt(3, new Point2d(minX - pad, maxY + pad), 0, 0, 0);
                        box.Closed = true;
                        box.LayerId = boxLayer;

                        btr.AppendEntity(box);
                        tr.AddNewlyCreatedDBObject(box, true);
                    }
                    tr.Commit();
                }
                doc.Editor.UpdateScreen();
            }
        }

        private ObjectId GetOrCreateLayer(Database db, Transaction tr, string layerName, short colorIndex)
        {
            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(layerName)) return lt[layerName];

            lt.UpgradeOpen();
            LayerTableRecord ltr = new LayerTableRecord { Name = layerName, Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex) };
            ObjectId id = lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
            return id;
        }

        // ★ 4. 그리드 클릭 줌 이동
        private void CourseDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CourseDataGrid.SelectedItem is CourseGroup selectedGroup)
            {
                if (selectedGroup.EoItems == null || selectedGroup.EoItems.Count == 0) return;

                Document doc = CadApp.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                Editor ed = doc.Editor;

                var startEo = selectedGroup.EoItems[0];
                Point2d center = new Point2d(startEo.NumX, startEo.NumY);

                if (!double.TryParse(TxtZoomSize.Text, out double viewSize))
                {
                    viewSize = 100.0;
                }

                using (doc.LockDocument())
                {
                    ViewTableRecord view = ed.GetCurrentView();
                    view.Width = viewSize;
                    view.Height = viewSize;
                    view.CenterPoint = center;
                    ed.SetCurrentView(view);
                    ed.UpdateScreen();
                }
            }
        }

        // ★ 5. 저장 내보내기 로직 (재촬영 옵션 포함 유지)
        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (CourseGroups.Count == 0)
            {
                await ShowModernDialog("알림", "저장할 데이터가 없습니다.");
                return;
            }

            bool isCsv = RbExcel.IsChecked == true;
            SaveFileDialog sfd = new SaveFileDialog { Filter = isCsv ? "CSV (*.csv)|*.csv" : "TXT (*.txt)|*.txt", FileName = "Updated_EO" };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(sfd.FileName))
                    {
                        string sep = isCsv ? "," : "\t";
                        string[] headers = { "ID_", "GPSTime(s)", "Easting(metres)", "Northing(metres)", "MSLHt(metres)", "Omega(deg)", "Phi(deg)", "Kap(deg)" };
                        sw.WriteLine(string.Join(sep, headers));

                        foreach (var group in CourseGroups)
                        {
                            string prefixRaw = group.NewCourseName?.Trim() ?? "";
                            string prefix = "";

                            if (int.TryParse(prefixRaw, out int parsedCourseNum)) prefix = $"{parsedCourseNum:D4}_";
                            else prefix = prefixRaw.EndsWith("_") ? prefixRaw : $"{prefixRaw}_";

                            int startNum = group.NewStartNumber;
                            string retakeSuffix = group.IsRetake ? "A" : "";

                            for (int i = 0; i < group.EoItems.Count; i++)
                            {
                                var eo = group.EoItems[i];
                                string newId = $"{prefix}{(startNum + i):D4}{retakeSuffix}";
                                sw.WriteLine($"{newId}{sep}{eo.GpsTime:F6}{sep}{eo.X}{sep}{eo.Y}{sep}{eo.Z}{sep}{eo.Omg}{sep}{eo.Phi}{sep}{eo.Kap}");
                            }
                        }
                    }
                    await ShowModernDialog("완료", "저장 성공!");
                    this.Close();
                }
                catch (Exception ex) { await ShowModernDialog("오류", ex.Message); }
            }
        }
    }

    // ==========================================
    // 모델 클래스
    // ==========================================
    public class EoData
    {
        public string OriginalId { get; set; }
        public double GpsTime { get; set; }
        public string X { get; set; }
        public string Y { get; set; }
        public string Z { get; set; }
        public string Omg { get; set; }
        public string Phi { get; set; }
        public string Kap { get; set; }
        public double NumX { get; set; }
        public double NumY { get; set; }
    }

    public class CourseGroup
    {
        public int CourseIndex { get; set; }
        public List<EoData> EoItems { get; set; } = new List<EoData>();
        public int PhotoCount => EoItems.Count;
        public string OldStartId { get; set; }
        public string NewCourseName { get; set; }
        public int NewStartNumber { get; set; } = 1;
        public bool IsRetake { get; set; } = false;

        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
    }
}