using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    public class DrawingItem
    {
        public string DisplayName { get; set; } // 화면에 보일 이름
        public Document Doc { get; set; }       // 실제 캐드 도면 객체 (새로만들기 등은 null)
        public bool IsSpecial { get; set; }     // 현재활성/새로만들기 구분용
    }
    public class ExcelPointData
    {
        public string Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    public partial class ExcelInputUI : FluentWindow
    {
        // ⭐️ [필수] 윈도우 API를 이용해 실행 중인 엑셀 강제로 잡기
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

        private ObservableCollection<ExcelPointData> _excelDataList;

        public ExcelInputUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

            _excelDataList = new ObservableCollection<ExcelPointData>();
            ExcelListView.ItemsSource = _excelDataList;
            RefreshDrawingList();


            BtnLoadExcel.Click += BtnLoadExcel_Click;
        }

        private void RefreshDrawingList()
        {
            CboDrawingSelect.Items.Clear();

            // 1. [고정] 현재 활성 도면 (Active Document)
            CboDrawingSelect.Items.Add(new DrawingItem
            {
                DisplayName = "▶ 현재 활성 도면 (Active)",
                Doc = CadApp.DocumentManager.MdiActiveDocument,
                IsSpecial = true
            });

            // 구분선 역할을 할 빈 아이템 하나 넣어주면 예쁩니다 (선택 불가하게 하거나 이름으로 구분)
            // CboDrawingSelect.Items.Add(new ComboBoxItem { Content = "------ 열린 도면 목록 ------", IsEnabled = false });

            // 2. [동적] 현재 캐드에 열려 있는 모든 도면 리스트
            foreach (Document doc in CadApp.DocumentManager)
            {
                // 이미 위에서 Active를 넣었지만, 전체 목록으로 한 번 더 보여주는 게 선택하기 편합니다.
                string activeTag = (doc == CadApp.DocumentManager.MdiActiveDocument) ? " (현재)" : "";
                CboDrawingSelect.Items.Add(new DrawingItem
                {
                    DisplayName = $"📄 {doc.Window.Text}{activeTag}",
                    Doc = doc,
                    IsSpecial = false
                });
            }

            // 3. [고정] 제일 아래에 새로 만들기
            CboDrawingSelect.Items.Add(new DrawingItem
            {
                DisplayName = "➕ 새로운 도면에 생성하기...",
                Doc = null,
                IsSpecial = true
            });

            // 콤보박스 표시 설정 (DisplayName이 보이도록)
            CboDrawingSelect.DisplayMemberPath = "DisplayName";
            CboDrawingSelect.SelectedIndex = 0; // 기본값: 현재 활성 도면
        }

        private void AddLog(string message, bool isError = false)
        {
            var tb = new System.Windows.Controls.TextBlock
            {
                Text = $"▶ {message}",
                FontSize = 11,
                Margin = new Thickness(2)
            };

            if (isError)
                tb.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
            else
                tb.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

            LogListBox.Items.Add(tb);
            LogListBox.ScrollIntoView(tb);
        }

        private void BtnLoadExcel_Click(object sender, RoutedEventArgs e)
        {
            dynamic excelApp = null;
            dynamic selectedRange = null;

            try
            {
                AddLog("엑셀 프로세스 연결 시도 중...");

                try
                {
                    excelApp = GetActiveObject("Excel.Application");
                }
                catch
                {
                    AddLog("실행 중인 엑셀을 찾을 수 없습니다. 엑셀을 먼저 열어주세요.", true);
                    return;
                }

                // 1. 엑셀 창을 활성화하고 플러그인 창을 잠시 치워줍니다.
                excelApp.Visible = true;

                AddLog("엑셀에서 데이터 범위를 드래그하세요...");

                // 2. 범위 선택 (Type:8). 엑셀 창이 뜨면서 한 번만 선택하게 됩니다.
                selectedRange = excelApp.InputBox(
                    "캐드에 입력할 ID, X, Y, Z 범위를 드래그하세요.",
                    "좌표 데이터 범위 지정",
                    Type: 8);

                // 3. 선택 직후 창 다시 복구
                this.WindowState = WindowState.Normal;
                this.Activate();

                // 엑셀 InputBox에서 취소 누르면 false(bool) 반환됨
                if (selectedRange is bool && (bool)selectedRange == false)
                {
                    AddLog("범위 선택이 취소되었습니다.");
                    return;
                }

                if (selectedRange == null) return;

                // 4. 데이터 파싱
                object[,] values = selectedRange.Value2 as object[,];

                if (values == null)
                {
                    AddLog("최소 4개의 열(ID, X, Y, Z)을 포함한 범위를 선택하세요.", true);
                    return;
                }

                int rowCount = values.GetLength(0);
                int colCount = values.GetLength(1);

                if (colCount < 4)
                {
                    AddLog($"열 부족: 현재 {colCount}열 선택됨 (4열 필요)", true);
                    return;
                }

                _excelDataList.Clear();
                int successCount = 0;

                for (int i = 1; i <= rowCount; i++)
                {
                    // 빈 줄 무시
                    if (values[i, 1] == null && values[i, 2] == null) continue;

                    try
                    {
                        var data = new ExcelPointData
                        {
                            Id = values[i, 1]?.ToString() ?? "",
                            X = double.TryParse(values[i, 2]?.ToString(), out double xVal) ? xVal : 0,
                            Y = double.TryParse(values[i, 3]?.ToString(), out double yVal) ? yVal : 0,
                            Z = double.TryParse(values[i, 4]?.ToString(), out double zVal) ? zVal : 0
                        };

                        // 헤더(문자열) 행 스킵 로직
                        if (i == 1 && !double.TryParse(data.Id, out _) && data.X == 0) continue;

                        _excelDataList.Add(data);
                        successCount++;
                    }
                    catch { }
                }

                // 5. UI 업데이트 및 컬럼 너비 자동 조절
                TxtDataCount.Text = $"{successCount} 개";
                AddLog($"{successCount}개의 데이터를 불러왔습니다.");

                if (ExcelListView.View is Wpf.Ui.Controls.GridView gv)
                {
                    foreach (var col in gv.Columns)
                    {
                        // Width를 NaN으로 설정하면 내용물에 맞춰 자동으로 늘어납니다.
                        if (double.IsNaN(col.Width)) col.Width = col.ActualWidth;
                        col.Width = double.NaN;
                    }
                }
            }
            catch (Exception ex)
            {
                this.WindowState = WindowState.Normal;
                AddLog($"오류: {ex.Message}", true);
            }
            finally
            {
                // 6. 메모리 해제 (selectedRange가 bool인 경우 해제하면 에러나므로 체크)
                if (selectedRange != null && !(selectedRange is bool))
                {
                    Marshal.ReleaseComObject(selectedRange);
                }
                if (excelApp != null)
                {
                    Marshal.ReleaseComObject(excelApp);
                }
            }
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            // 1. 데이터 확인
            if (_excelDataList == null || _excelDataList.Count == 0)
            {
                AddLog("입력할 데이터가 없습니다. 엑셀에서 먼저 로드하세요.", true);
                return;
            }

            // 2. 작업 대상 도면 가져오기
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

            // 3. UI 설정값 읽기
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
                        // ⭐️ 텍스트는 기본색(7번: 흰/검), 주점(_PP)은 빨간색(1번)으로 지정!
                        ObjectId txtLyrId = GetOrCreateLayer(db, tr, textLayer, 7);
                        ObjectId pntLyrId = GetOrCreateLayer(db, tr, pointLayer, 1);

                        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                        // ⭐️ 에러 해결: 범위를 계산할 변수
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

                            // ⭐️ 에러 해결: 오류 나던 AddEntity 대신 가장 확실한 AddPoint 사용
                            // 데이터의 좌표 자체를 전체 영역 박스에 포함시킵니다.
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

                        // 4. 화면 갱신 및 줌 (Zoom Extents)
                        if (hasData)
                        {
                            ed.UpdateScreen();

                            ViewTableRecord view = ed.GetCurrentView();
                            double width = totalExtents.MaxPoint.X - totalExtents.MinPoint.X;
                            double height = totalExtents.MaxPoint.Y - totalExtents.MinPoint.Y;

                            // 만약 데이터가 1개밖에 없어서 width/height가 0이 될 경우를 대비한 방어 코드
                            if (width == 0) width = textHeight * 10;
                            if (height == 0) height = textHeight * 10;

                            Point2d center = new Point2d((totalExtents.MaxPoint.X + totalExtents.MinPoint.X) / 2.0,
                                                         (totalExtents.MaxPoint.Y + totalExtents.MinPoint.Y) / 2.0);

                            view.CenterPoint = center;
                            // 글자 크기나 원의 크기를 고려해서 화면 여유 공간을 1.5배로 넉넉하게 잡습니다.
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

        // 레이어 존재 확인 및 자동 생성 헬퍼 함수
        private ObjectId GetOrCreateLayer(Database db, Transaction tr, string layerName, short colorIndex = 7)
        {
            LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

            // 레이어가 이미 존재하는 경우
            if (lt.Has(layerName))
            {
                ObjectId lyrId = lt[layerName];
                return lyrId;
            }

            // 레이어가 없어서 새로 만드는 경우
            lt.UpgradeOpen();
            LayerTableRecord ltr = new LayerTableRecord();
            ltr.Name = layerName;

            // ⭐️ 엑셀 데이터용 레이어 색상 지정
            ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex);

            ObjectId id = lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
            return id;
        }
    }
}