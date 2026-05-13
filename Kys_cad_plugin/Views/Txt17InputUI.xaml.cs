using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Controls;

// 오토캐드 API 참조
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
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

        // TXT/CSV 파일 로드 로직 (FileShare.ReadWrite 적용)
        private async void BtnLoadTxt_Click(object sender, RoutedEventArgs e)
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
                    _pointDataList.Clear();
                    int successCount = 0;

                    using (FileStream fs = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs, Encoding.Default))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;

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

                                    if (successCount == 0 && !double.TryParse(parts[1], out _) && data.X == 0) continue;

                                    _pointDataList.Add(data);
                                    successCount++;
                                }
                                catch { }
                            }
                        }
                    }

                    // 컬럼 너비 자동 맞춤 기능
                    if (DataListView.View is System.Windows.Controls.GridView gv)
                    {
                        foreach (var col in gv.Columns)
                        {
                            if (double.IsNaN(col.Width)) col.Width = col.ActualWidth;
                            col.Width = double.NaN;
                        }
                    }

                    await ShowModernDialog("로드 완료", $"{openFileDialog.SafeFileName} 파일에서 {successCount}개의 데이터를 불러왔습니다.");
                }
                catch (Exception ex)
                {
                    await ShowModernDialog("파일 읽기 오류", ex.Message);
                }
            }
        }

        // [Excel17InputUI 도면 입력 로직 완벽 복제]
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

            string circleColorStr = (CboCircleColor.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";
            string fillColorStr = (CboFillColor.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";
            string textColorStr = (CboTextColor.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "";

            short circleColorIdx = GetColorIndex(circleColorStr);
            short fillColorIdx = GetColorIndex(fillColorStr);
            short textColorIdx = GetColorIndex(textColorStr);
            bool useFill = !fillColorStr.Contains("None");

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

                            // 1. 원 생성 (선 가중치 0.40mm)
                            Circle circ = new Circle();
                            circ.Center = center;
                            circ.Radius = circleRad;
                            circ.LayerId = lyrId;
                            circ.ColorIndex = circleColorIdx;
                            circ.LineWeight = LineWeight.LineWeight040;

                            btr.AppendEntity(circ);
                            tr.AddNewlyCreatedDBObject(circ, true);

                            // 2. 해치(채우기) 생성
                            if (useFill)
                            {
                                Hatch hat = new Hatch();
                                hat.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                                hat.LayerId = lyrId;
                                hat.ColorIndex = fillColorIdx;

                                btr.AppendEntity(hat);
                                tr.AddNewlyCreatedDBObject(hat, true);

                                ObjectIdCollection ids = new ObjectIdCollection();
                                ids.Add(circ.ObjectId);
                                hat.AppendLoop(HatchLoopTypes.Default, ids);
                                hat.EvaluateHatch(true);
                            }

                            // 3. 텍스트 생성 (정중앙 정렬)
                            DBText txt = new DBText();
                            txt.TextString = data.Id;
                            txt.Height = textH;
                            txt.LayerId = lyrId;
                            txt.ColorIndex = textColorIdx;
                            txt.Justify = AttachmentPoint.MiddleCenter;
                            txt.AlignmentPoint = center;

                            btr.AppendEntity(txt);
                            tr.AddNewlyCreatedDBObject(txt, true);

                            textObjIds.Add(txt.ObjectId);
                        }

                        // 텍스트를 위로 올리기
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
            catch (Exception ex)
            {
                await ShowModernDialog("캐드 입력 오류", ex.Message);
            }
        }

        private short GetColorIndex(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text) || text.Contains("None")) return 7;
                int start = text.IndexOf('(');
                int end = text.IndexOf(')');
                if (start != -1 && end != -1)
                {
                    string num = text.Substring(start + 1, end - start - 1);
                    return short.Parse(num);
                }
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