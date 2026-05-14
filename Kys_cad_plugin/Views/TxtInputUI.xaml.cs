using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Microsoft.Win32;
using System;
using System.Collections.Generic; // List 사용을 위해 추가
using System.Collections.ObjectModel;
using System.IO;
using System.Linq; // Max 등 LINQ 사용을 위해 추가
using System.Text;
using System.Windows;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Kys_cad_plugin.Core; // ★ 중앙 데이터 매니저 참조

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

        // [수정] 파일 입력 연동 (DataImportManager 사용)
        private async void BtnLoadTxt_Click(object sender, RoutedEventArgs e)
        {
            // 1. 필요한 필드 정의
            var targetFields = new List<string> { "ID (점 번호)", "X 좌표", "Y 좌표", "Z 고도" };

            // 2. 중앙 매니저 호출 (매핑 창에서 유저가 선택)
            var result = await DataImportManager.ImportAndMap(this, targetFields);

            if (result != null && result.Rows.Count > 0)
            {
                _excelDataList.Clear();
                int count = 0;

                foreach (var row in result.Rows)
                {
                    try
                    {
                        var data = new ExcelPointData
                        {
                            Id = row["ID (점 번호)"],
                            X = double.Parse(row["X 좌표"]),
                            Y = double.Parse(row["Y 좌표"]),
                            Z = double.Parse(row["Z 고도"])
                        };
                        _excelDataList.Add(data);
                        count++;
                    }
                    catch { /* 헤더 등 스킵 */ }
                }

                TxtDataCount.Text = $"{count} 개";
                AddLog($"{result.FileName} 로드 완료. ({count}개 데이터 추출)");

                // 그리드 컬럼 너비 자동 맞춤
                if (ExcelListView.View is System.Windows.Controls.GridView gv)
                {
                    foreach (var col in gv.Columns)
                    {
                        if (double.IsNaN(col.Width)) col.Width = col.ActualWidth;
                        col.Width = double.NaN;
                    }
                }
            }
        }

        // [복원 및 연동] ExcelInputUI와 100% 동일한 캐드 생성 로직
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

            // ExcelInputUI의 로직 그대로 사용 (TT/PP 레이어 분리)
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
                        // TT 레이어는 7번(흰/검), PP 레이어는 1번(빨간색) 지정
                        ObjectId txtLyrId = GetOrCreateLayer(db, tr, textLayer, 7);
                        ObjectId pntLyrId = GetOrCreateLayer(db, tr, pointLayer, 1);

                        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                        Extents3d totalExtents = new Extents3d();
                        bool hasData = false;

                        foreach (var data in _excelDataList)
                        {
                            Point3d pos = new Point3d(data.X, data.Y, data.Z);

                            // 텍스트 생성 로직
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

                            // 주점 생성 로직 (Circle 사용)
                            if (inputMode == 0 || inputMode == 2)
                            {
                                Circle acCircle = new Circle();
                                acCircle.LayerId = pntLyrId;
                                acCircle.Center = pos;
                                acCircle.Radius = pointRadius;

                                btr.AppendEntity(acCircle);
                                tr.AddNewlyCreatedDBObject(acCircle, true);
                            }

                            // 전체 영역 박스 계산 (Zoom Extents용)
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

                        tr.Commit();
                        AddLog($"{_excelDataList.Count}개의 데이터 입력 성공!");

                        // 자동 줌(Zoom Extents) 로직 실행
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

        // 레이어 생성 헬퍼 (ExcelInputUI와 동일)
        private ObjectId GetOrCreateLayer(Database db, Transaction tr, string layerName, short colorIndex = 7)
        {
            LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            if (lt.Has(layerName)) return lt[layerName];

            lt.UpgradeOpen();
            LayerTableRecord ltr = new LayerTableRecord { Name = layerName };
            ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex);
            ObjectId id = lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
            return id;
        }
    }
}