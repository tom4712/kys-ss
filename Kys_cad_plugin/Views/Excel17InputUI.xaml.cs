// 오토캐드 API 참조
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    public partial class Excel17InputUI : FluentWindow
    {
        [DllImport("ole32.dll")]
        private static extern int CLSIDFromProgID([MarshalAs(UnmanagedType.LPWStr)] string lpszProgID, out Guid pclsid);

        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.Interface)] out object ppunk);

        private static object GetActiveObject(string progId)
        {
            Guid clsid;
            CLSIDFromProgID(progId, out clsid);
            GetActiveObject(ref clsid, IntPtr.Zero, out object obj);
            return obj;
        }

        private ObservableCollection<ExcelPointData> _excelDataList = new ObservableCollection<ExcelPointData>();

        public Excel17InputUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
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

        private async void BtnLoadExcel_Click(object sender, RoutedEventArgs e)
        {
            dynamic excelApp = null;
            dynamic selectedRange = null;
            try
            {
                try { excelApp = GetActiveObject("Excel.Application"); }
                catch { await ShowModernDialog("연결 실패", "실행 중인 엑셀을 찾을 수 없습니다. 엑셀을 먼저 열어주세요."); return; }

                this.WindowState = WindowState.Minimized;
                excelApp.Visible = true;

                selectedRange = excelApp.InputBox("캐드에 입력할 ID, X, Y, Z 범위를 드래그하여 선택하세요.", "17사 좌표 데이터 범위 지정", Type: 8);

                this.WindowState = WindowState.Normal;
                this.Activate();

                if (selectedRange is bool && (bool)selectedRange == false) return;
                if (selectedRange == null) return;

                object[,] values = selectedRange.Value2 as object[,];
                if (values == null)
                {
                    await ShowModernDialog("데이터 오류", "선택된 범위에서 데이터를 읽을 수 없습니다.");
                    return;
                }

                _excelDataList.Clear();
                int successCount = 0;

                for (int i = 1; i <= values.GetLength(0); i++)
                {
                    if (values[i, 1] == null && values[i, 2] == null) continue;

                    try
                    {
                        var data = new ExcelPointData
                        {
                            Id = values[i, 1]?.ToString() ?? "",
                            X = double.TryParse(values[i, 2]?.ToString(), out double x) ? x : 0,
                            Y = double.TryParse(values[i, 3]?.ToString(), out double y) ? y : 0,
                            Z = double.TryParse(values[i, 4]?.ToString(), out double z) ? z : 0
                        };

                        if (i == 1 && !double.TryParse(data.X.ToString(), out _) && data.X == 0) continue;

                        _excelDataList.Add(data);
                        successCount++;
                    }
                    catch { }
                }

                await ShowModernDialog("로드 완료", $"{successCount}개의 데이터를 그리드뷰에 불러왔습니다.");
            }
            catch (Exception ex)
            {
                this.WindowState = WindowState.Normal;
                await ShowModernDialog("오류 발생", $"에러 내용: {ex.Message}");
            }
            finally
            {
                if (selectedRange != null && !(selectedRange is bool)) Marshal.ReleaseComObject(selectedRange);
                if (excelApp != null) Marshal.ReleaseComObject(excelApp);
            }
        }

        private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            if (_excelDataList.Count == 0)
            {
                await ShowModernDialog("알림", "입력할 데이터가 없습니다. 먼저 엑셀에서 데이터를 로드하세요.");
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
                    await ShowModernDialog("도면 생성 실패", $"새 도면을 생성하는 중 오류가 발생했습니다: {ex.Message}");
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

                        foreach (var data in _excelDataList)
                        {
                            Point3d center = new Point3d(data.X, data.Y, data.Z);

                            // 1. 원 생성
                            Circle circ = new Circle();
                            circ.Center = center;
                            circ.Radius = circleRad;
                            circ.LayerId = lyrId;
                            circ.ColorIndex = circleColorIdx;

                            // [수정됨] 선 가중치(두께) 설정: 0.40mm 로 조금 더 두껍게 설정
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

                            // 3. 텍스트 생성
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

                        if (textObjIds.Count > 0)
                        {
                            DrawOrderTable dot = tr.GetObject(btr.DrawOrderTableId, OpenMode.ForWrite) as DrawOrderTable;
                            dot.MoveToTop(textObjIds);
                        }

                        tr.Commit();
                        await ShowModernDialog("입력 성공", $"{_excelDataList.Count}개의 데이터를 [{doc.Window.Text}] 도면에 생성했습니다.");
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