// 오토캐드 API 참조
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Kys_cad_plugin.Core; // 중앙 매니저 참조
using System.Collections.ObjectModel;
using System.Windows;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    public partial class Txt17InputUI : FluentWindow
    {
        private ObservableCollection<ExcelPointData> _pointDataList = new ObservableCollection<ExcelPointData>();

        public Txt17InputUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
            DataListView.ItemsSource = _pointDataList;
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

        // ★ 중앙 집중식 파일 로드 호출 부분
        private async void BtnLoadTxt_Click(object sender, RoutedEventArgs e)
        {
            // 1. 이 UI에서 필요한 데이터 필드 정의 (순서대로 콤보박스가 생성됨)
            var targetFields = new List<string> { "ID (이름)", "X 좌표", "Y 좌표", "Z 고도" };

            // 2. 중앙 매니저에게 모든 작업을 위임 (파일열기, 매핑팝업, 파싱)
            var result = await DataImportManager.ImportAndMap(this, targetFields);

            // 3. 결과가 돌아오면 UI 리스트에 바인딩
            if (result != null && result.Rows.Count > 0)
            {
                _pointDataList.Clear();
                foreach (var row in result.Rows)
                {
                    try
                    {
                        _pointDataList.Add(new ExcelPointData
                        {
                            // 딕셔너리 키는 위에서 정의한 targetFields의 이름과 동일함
                            Id = row["ID (이름)"],
                            X = double.Parse(row["X 좌표"]),
                            Y = double.Parse(row["Y 좌표"]),
                            Z = double.Parse(row["Z 고도"])
                        });
                    }
                    catch { }
                }

                if (DataListView.View is System.Windows.Controls.GridView gv)
                {
                    foreach (var col in gv.Columns) col.Width = col.ActualWidth;
                    foreach (var col in gv.Columns) col.Width = double.NaN;
                }

                await ShowModernDialog("임포트 완료", $"{result.FileName} 파일에서 {result.SuccessCount}개의 데이터를 성공적으로 분류했습니다.");
            }
        }

        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_pointDataList.Count == 0)
            {
                await ShowModernDialog("알림", "입력할 데이터가 없습니다. 먼저 텍스트 파일을 로드하세요.");
                return;
            }

            var selectedItem = CboDrawingSelect.SelectedItem as DrawingItem;
            if (selectedItem == null) return;

            Document doc = null;
            if (selectedItem.Doc == null || selectedItem.DisplayName.Contains("새로운 도면"))
            {
                try
                {
                    doc = CadApp.DocumentManager.Add("");
                    CadApp.DocumentManager.MdiActiveDocument = doc;
                }
                catch (Exception ex)
                {
                    await ShowModernDialog("도면 생성 실패", $"새 도면 생성 오류: {ex.Message}");
                    return;
                }
            }
            else
            {
                doc = selectedItem.Doc;
            }

            if (doc == null) return;

            Database db = doc.Database;
            string layerName = string.IsNullOrWhiteSpace(TxtLayerName.Text) ? "17_COORDINATES" : TxtLayerName.Text;
            double circleRad = double.TryParse(TxtCircleSize.Text, out double r) ? r : 50.0;
            double textH = double.TryParse(TxtTextSize.Text, out double h) ? h : 100.0;

            short circleColorIdx = GetColorIndex((CboCircleColor.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString());
            short fillColorIdx = GetColorIndex((CboFillColor.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString());
            short textColorIdx = GetColorIndex((CboTextColor.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString());
            bool useFill = !((CboFillColor.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "").Contains("None");

            try
            {
                using (DocumentLock loc = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        ObjectId lyrId = GetOrCreateLayer(db, tr, layerName);
                        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                        ObjectIdCollection textObjIds = new ObjectIdCollection();

                        foreach (var data in _pointDataList)
                        {
                            Point3d center = new Point3d(data.X, data.Y, data.Z);
                            Circle circ = new Circle { Center = center, Radius = circleRad, LayerId = lyrId, ColorIndex = circleColorIdx, LineWeight = LineWeight.LineWeight040 };
                            btr.AppendEntity(circ);
                            tr.AddNewlyCreatedDBObject(circ, true);

                            if (useFill)
                            {
                                Hatch hat = new Hatch();
                                hat.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                                hat.LayerId = lyrId;
                                hat.ColorIndex = fillColorIdx;
                                btr.AppendEntity(hat);
                                tr.AddNewlyCreatedDBObject(hat, true);
                                hat.AppendLoop(HatchLoopTypes.Default, new ObjectIdCollection { circ.ObjectId });
                                hat.EvaluateHatch(true);
                            }

                            DBText txt = new DBText { TextString = data.Id, Height = textH, LayerId = lyrId, ColorIndex = textColorIdx, Justify = AttachmentPoint.MiddleCenter, AlignmentPoint = center };
                            btr.AppendEntity(txt);
                            tr.AddNewlyCreatedDBObject(txt, true);
                            textObjIds.Add(txt.ObjectId);
                        }

                        if (textObjIds.Count > 0)
                        {
                            DrawOrderTable dot = tr.GetObject(btr.DrawOrderTableId, OpenMode.ForWrite) as DrawOrderTable;
                            dot.MoveToTop(textObjIds);
                        }

                        tr.Commit();
                        await ShowModernDialog("입력 성공", $"{_pointDataList.Count}개의 데이터를 [{doc.Window.Text}] 도면에 생성했습니다.");
                    }
                }
            }
            catch (Exception ex) { await ShowModernDialog("캐드 입력 오류", ex.Message); }
        }

        private short GetColorIndex(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text) || text.Contains("None")) return 7;
                int start = text.IndexOf('('); int end = text.IndexOf(')');
                if (start != -1 && end != -1) return short.Parse(text.Substring(start + 1, end - start - 1));
            }
            catch { }
            return 7;
        }

        private ObjectId GetOrCreateLayer(Database db, Transaction tr, string name)
        {
            LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            if (lt.Has(name)) return lt[name];
            lt.UpgradeOpen();
            LayerTableRecord ltr = new LayerTableRecord { Name = name };
            ObjectId id = lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
            return id;
        }

        private async System.Threading.Tasks.Task ShowModernDialog(string title, string content)
        {
            var msgBox = new Wpf.Ui.Controls.MessageBox { Title = title, Content = new System.Windows.Controls.TextBlock { Text = content, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10) }, CloseButtonText = "확인", Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Width = 400, Height = 200 };
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(msgBox);
            await msgBox.ShowDialogAsync();
        }
    }
}