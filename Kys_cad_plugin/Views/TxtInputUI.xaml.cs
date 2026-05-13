using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    public partial class TxtInputUI : FluentWindow
    {
        private ObservableCollection<ExcelPointData> _excelDataList;

        public TxtInputUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

            _excelDataList = new ObservableCollection<ExcelPointData>();
            ExcelListView.ItemsSource = _excelDataList;
            RefreshDrawingList();
        }

        private void RefreshDrawingList()
        {
            CboDrawingSelect.Items.Clear();
            CboDrawingSelect.Items.Add(new DrawingItem { DisplayName = "▶ 현재 활성 도면 (Active)", Doc = CadApp.DocumentManager.MdiActiveDocument });

            foreach (Document doc in CadApp.DocumentManager)
            {
                string activeTag = (doc == CadApp.DocumentManager.MdiActiveDocument) ? " (현재)" : "";
                CboDrawingSelect.Items.Add(new DrawingItem { DisplayName = $"📄 {doc.Window.Text}{activeTag}", Doc = doc });
            }

            CboDrawingSelect.Items.Add(new DrawingItem { DisplayName = "➕ 새로운 도면에 생성하기...", Doc = null });
            CboDrawingSelect.SelectedIndex = 0;
        }

        private void AddLog(string message, bool isError = false)
        {
            var tb = new System.Windows.Controls.TextBlock { Text = $"▶ {message}", FontSize = 11, Margin = new Thickness(2) };
            if (isError)
                tb.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
            else
                tb.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

            LogListBox.Items.Add(tb);
            LogListBox.ScrollIntoView(tb);
        }

        // [변경 핵심] FileShare.ReadWrite를 사용하여 다른 프로그램에서 열려있어도 강제로 읽어옴
        private void BtnLoadTxt_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "좌표 데이터 파일 (*.txt;*.csv)|*.txt;*.csv|모든 파일 (*.*)|*.*",
                Title = "좌표 데이터 파일 선택"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    AddLog("파일 분석 중...");
                    _excelDataList.Clear();
                    int successCount = 0;

                    // 다른 프로세스가 열고 있어도 읽을 수 있도록 FileShare.ReadWrite 적용
                    using (FileStream fs = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs, System.Text.Encoding.Default))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            // 쉼표, 탭, 띄어쓰기를 기준으로 데이터 분할
                            string[] parts = line.Split(new char[] { ',', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                            if (parts.Length >= 4)
                            {
                                try
                                {
                                    var data = new ExcelPointData
                                    {
                                        Id = parts[0],
                                        X = double.Parse(parts[1]),
                                        Y = double.Parse(parts[2]),
                                        Z = double.Parse(parts[3])
                                    };

                                    // 헤더 텍스트 줄이면 건너뛰기
                                    if (successCount == 0 && !double.TryParse(parts[1], out _) && data.X == 0) continue;

                                    _excelDataList.Add(data);
                                    successCount++;
                                }
                                catch { /* 숫자 파싱 실패 시 다음 줄로 넘어감 */ }
                            }
                        }
                    }

                    // UI 업데이트 (데이터 수 표시)
                    TxtDataCount.Text = $"{successCount} 개";
                    AddLog($"{openFileDialog.SafeFileName} 로드 완료. ({successCount}개 데이터 추출)");

                    // [추가됨] 데이터 로드 후 GridView 컬럼 너비 자동 맞춤 기능 복구
                    if (ExcelListView.View is System.Windows.Controls.GridView gv)
                    {
                        foreach (var col in gv.Columns)
                        {
                            // Width를 NaN으로 설정하면 내용물의 길이에 맞춰 자동으로 쫙 늘어납니다.
                            if (double.IsNaN(col.Width)) col.Width = col.ActualWidth;
                            col.Width = double.NaN;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"파일 읽기 오류: {ex.Message}", true);
                }
            }
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_excelDataList == null || _excelDataList.Count == 0)
            {
                AddLog("입력할 데이터가 없습니다. 먼저 로드하세요.", true);
                return;
            }

            var selectedItem = CboDrawingSelect.SelectedItem as DrawingItem;
            if (selectedItem == null) return;

            Document doc = null;
            if (selectedItem.DisplayName.Contains("새로운 도면"))
            {
                doc = CadApp.DocumentManager.Add("");
                CadApp.DocumentManager.MdiActiveDocument = doc;
            }
            else
            {
                doc = selectedItem.Doc;
            }

            if (doc == null) return;

            // UI 설정값 읽기
            string baseLayerName = string.IsNullOrWhiteSpace(TxtLayerName.Text) ? "0" : TxtLayerName.Text;
            string textLayer = baseLayerName + "_TT";
            string pointLayer = baseLayerName + "_PP";

            double textHeight = double.TryParse(TxtTextHeight.Text, out double th) ? th : 100.0;
            double pointRadius = double.TryParse(TxtPointSize.Text, out double ps) ? ps : 50.0;
            double rotationDeg = double.TryParse(TxtRotation.Text, out double rd) ? rd : 0.0;
            double rotationRad = rotationDeg * (Math.PI / 180.0);

            int inputMode = CboInputType.SelectedIndex; // 0:둘다, 1:텍스트만, 2:주점만

            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                AddLog($"{doc.Window.Text} 도면에 쓰기 시작...");

                using (DocumentLock loc = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        // 텍스트는 7번(흰/검), 주점(_PP)은 1번(빨간색)으로 지정
                        ObjectId txtLyrId = GetOrCreateLayer(db, tr, textLayer, 7);
                        ObjectId pntLyrId = GetOrCreateLayer(db, tr, pointLayer, 1);

                        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                        Extents3d totalExtents = new Extents3d();
                        bool hasData = false;

                        foreach (var data in _excelDataList)
                        {
                            Point3d pos = new Point3d(data.X, data.Y, data.Z);

                            // 텍스트 생성
                            if (inputMode == 0 || inputMode == 1)
                            {
                                DBText acText = new DBText();
                                acText.LayerId = txtLyrId;
                                acText.Height = textHeight;
                                acText.Rotation = rotationRad;
                                acText.TextString = data.Id;
                                acText.Position = pos;

                                btr.AppendEntity(acText);
                                tr.AddNewlyCreatedDBObject(acText, true);
                            }

                            // 주점 생성 (원)
                            if (inputMode == 0 || inputMode == 2)
                            {
                                Circle acCircle = new Circle();
                                acCircle.LayerId = pntLyrId;
                                acCircle.Center = pos;
                                acCircle.Radius = pointRadius;

                                btr.AppendEntity(acCircle);
                                tr.AddNewlyCreatedDBObject(acCircle, true);
                            }

                            // Zoom Extents 계산용 박스 확보
                            if (!hasData)
                            {
                                totalExtents = new Extents3d(pos, pos);
                                hasData = true;
                            }
                            else
                            {
                                totalExtents.AddPoint(pos);
                            }
                        }

                        tr.Commit(); // 도면에 저장
                        AddLog($"{_excelDataList.Count}개의 데이터 입력 성공!");

                        // 화면 갱신 및 줌 (Zoom Extents)
                        if (hasData)
                        {
                            ed.UpdateScreen();

                            ViewTableRecord view = ed.GetCurrentView();
                            double width = totalExtents.MaxPoint.X - totalExtents.MinPoint.X;
                            double height = totalExtents.MaxPoint.Y - totalExtents.MinPoint.Y;

                            if (width == 0) width = textHeight * 10;
                            if (height == 0) height = textHeight * 10;

                            Point2d center = new Point2d((totalExtents.MaxPoint.X + totalExtents.MinPoint.X) / 2.0,
                                                         (totalExtents.MaxPoint.Y + totalExtents.MinPoint.Y) / 2.0);

                            view.CenterPoint = center;
                            view.Width = width * 1.5;
                            view.Height = height * 1.5;
                            ed.SetCurrentView(view);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                AddLog($"캐드 입력 중 오류: {ex.Message}", true);
            }
        }

        // 레이어 생성 헬퍼 함수
        private ObjectId GetOrCreateLayer(Database db, Transaction tr, string layerName, short colorIndex = 7)
        {
            LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            if (lt.Has(layerName)) return lt[layerName];

            lt.UpgradeOpen();
            LayerTableRecord ltr = new LayerTableRecord();
            ltr.Name = layerName;
            ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex);

            ObjectId id = lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
            return id;
        }
    }
}