// 오토캐드 API 참조
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using Wpf.Ui.Controls;

// 오토캐드 API 참조
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
namespace Kys_cad_plugin.Views
{
    // 1. 리스트뷰에 바인딩할 데이터 모델
    public class CollectedTextData
    {
        public string Text { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string LayerName { get; set; }
    }

    public partial class ExcelCollectUI : FluentWindow
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
        // UI가 자동으로 업데이트되는 컬렉션
        private ObservableCollection<CollectedTextData> _collectedDataList;

        public ExcelCollectUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

            _collectedDataList = new ObservableCollection<CollectedTextData>();
            CollectListView.ItemsSource = _collectedDataList;


        }

        // 도면에서 수집 버튼 클릭
        private void BtnCollect_Click(object sender, RoutedEventArgs e)
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            // 1. 캐드 화면을 편하게 선택할 수 있도록 플러그인 창을 잠시 내립니다.
            this.WindowState = WindowState.Minimized;

            try
            {
                // 2. 선택 필터 설정: 일반 텍스트(TEXT)와 다중 텍스트(MTEXT)만 선택되도록 제한
                TypedValue[] tvs = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<OR"),
                    new TypedValue((int)DxfCode.Start, "TEXT"),
                    new TypedValue((int)DxfCode.Start, "MTEXT"),
                    new TypedValue((int)DxfCode.Operator, "OR>")
                };
                SelectionFilter filter = new SelectionFilter(tvs);

                // 3. 사용자에게 범위 지정 요청
                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n수집할 텍스트 영역을 드래그하세요 (완료 시 Enter): ";

                PromptSelectionResult psr = ed.GetSelection(pso, filter);

                // 4. 정상적으로 범위를 지정하고 Enter를 쳤을 경우
                if (psr.Status == PromptStatus.OK && psr.Value != null)
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        _collectedDataList.Clear();
                        int count = 0;

                        foreach (SelectedObject so in psr.Value)
                        {
                            Entity ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;

                            if (ent is DBText dbText) // 단일 텍스트(TEXT)인 경우
                            {
                                _collectedDataList.Add(new CollectedTextData
                                {
                                    Text = dbText.TextString,
                                    // 보기 좋게 소수점 3자리까지만 자르기
                                    X = Math.Round(dbText.Position.X, 3),
                                    Y = Math.Round(dbText.Position.Y, 3),
                                    Z = Math.Round(dbText.Position.Z, 3),
                                    LayerName = dbText.Layer
                                });
                                count++;
                            }
                            else if (ent is MText mText) // 다중 텍스트(MTEXT)인 경우
                            {
                                _collectedDataList.Add(new CollectedTextData
                                {
                                    Text = mText.Text, // 캐드의 복잡한 서식 기호를 뺀 순수 텍스트
                                    X = Math.Round(mText.Location.X, 3),
                                    Y = Math.Round(mText.Location.Y, 3),
                                    Z = Math.Round(mText.Location.Z, 3),
                                    LayerName = mText.Layer
                                });
                                count++;
                            }
                        }

                        // 5. UI 수집 갯수 갱신
                        TxtCollectedCount.Text = count.ToString();
                        tr.Commit();
                    }
                }
                else
                {
                    ed.WriteMessage("\n텍스트 수집이 취소되었습니다.");
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"캐드 데이터 수집 중 오류: {ex.Message}", "오류");
            }
            finally
            {
                // 6. 작업이 끝나면 (선택 완료 또는 취소) 창을 다시 화면에 띄웁니다.
                this.WindowState = WindowState.Normal;
                this.Activate();
            }
        }

        // 2. 엑셀로 출력하기 버튼 클릭 (async 추가 및 바쁨 에러 방어 로직 포함)
        // 2. 엑셀로 출력하기 버튼 클릭 (모던 다이얼로그 + 바쁨 에러 방어 + 변수명 수정 완료)
        private async void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_collectedDataList.Count == 0)
            {
                await ShowModernDialog("알림", "출력할 데이터가 없습니다. 먼저 도면에서 수집해 주세요.");
                return;
            }

            dynamic excelApp = null;
            dynamic startCell = null;

            try
            {
                // ⭐️ dynamic 방식을 통한 엑셀 프로세스 획득
                try
                {
                    excelApp = GetActiveObject("Excel.Application");
                }
                catch
                {
                    await ShowModernDialog("오류", "실행 중인 엑셀이 없습니다. 엑셀 창을 먼저 열어주세요.");
                    return;
                }

                excelApp.Visible = true;
                this.WindowState = WindowState.Minimized;

                // ⭐️ dynamic을 활용한 InputBox 호출
                startCell = excelApp.InputBox(
                    "데이터를 출력할 시작 셀(왼쪽 상단)을 클릭하세요.",
                    "엑셀 출력 위치 지정",
                    Type: 8);

                this.WindowState = WindowState.Normal;
                this.Activate();

                // 취소 버튼 누른 경우 처리
                if (startCell is bool && (bool)startCell == false) return;

                // 출력 항목 구성
                System.Collections.Generic.List<string> headers = new System.Collections.Generic.List<string>();
                if (ChkText.IsChecked == true) headers.Add("Text");
                if (ChkX.IsChecked == true) headers.Add("X 좌표");
                if (ChkY.IsChecked == true) headers.Add("Y 좌표");
                if (ChkZ.IsChecked == true) headers.Add("Z 좌표");
                if (ChkLayer.IsChecked == true) headers.Add("도면층 (Layer) 이름");

                if (headers.Count == 0)
                {
                    await ShowModernDialog("경고", "출력할 항목을 하나 이상 선택해야 합니다.");
                    return;
                }

                // 데이터 배열 생성
                int rowCount = _collectedDataList.Count + 1;
                int colCount = headers.Count;
                object[,] outputValues = new object[rowCount, colCount];

                // 헤더 채우기
                for (int c = 0; c < colCount; c++)
                {
                    outputValues[0, c] = headers[c];
                }

                // 데이터 채우기
                for (int i = 0; i < _collectedDataList.Count; i++)
                {
                    var data = _collectedDataList[i];
                    int colIndex = 0;

                    if (ChkText.IsChecked == true) outputValues[i + 1, colIndex++] = data.Text;
                    if (ChkX.IsChecked == true) outputValues[i + 1, colIndex++] = data.X;
                    if (ChkY.IsChecked == true) outputValues[i + 1, colIndex++] = data.Y;
                    if (ChkZ.IsChecked == true) outputValues[i + 1, colIndex++] = data.Z;
                    if (ChkLayer.IsChecked == true) outputValues[i + 1, colIndex++] = data.LayerName;
                }

                // ⭐️ 변수명 충돌 해결 (r, c -> startRow, startCol)
                dynamic sheet = startCell.Worksheet;
                int startRow = startCell.Row;
                int startCol = startCell.Column;
                dynamic endCell = sheet.Cells[startRow + rowCount - 1, startCol + colCount - 1];
                dynamic targetRange = sheet.Range[startCell, endCell];

                bool isSuccess = false;
                int retryCount = 0;

                // ⭐️ 엑셀이 바쁠 경우(0x800AC472) 최대 10번(약 5초)까지 재시도
                while (!isSuccess && retryCount < 10)
                {
                    try
                    {
                        targetRange.Value2 = outputValues;
                        targetRange.Borders.LineStyle = 0; // 테두리 선
                        targetRange.Columns.AutoFit();     // 자동 너비 조절
                        isSuccess = true;                  // 에러 없이 통과하면 성공!
                    }
                    catch (Exception ex)
                    {
                        // 에러 메시지나 HResult에 0x800AC472 코드가 포함되어 있는지 확인
                        if (ex.HResult == unchecked((int)0x800AC472) || ex.Message.Contains("0x800AC472"))
                        {
                            retryCount++;
                            // 0.5초 대기 후 다시 시도 (비동기 대기)
                            await System.Threading.Tasks.Task.Delay(500);
                        }
                        else
                        {
                            throw; // 다른 진짜 에러라면 루프를 빠져나가서 아래 catch 문으로 던짐
                        }
                    }
                }

                if (!isSuccess)
                {
                    await ShowModernDialog("엑셀 응답 없음", "엑셀이 계속 셀 편집 모드에 있어 데이터를 입력할 수 없습니다.\n엑셀 화면을 클릭하고 [ESC] 키를 누른 후 다시 시도해 주세요.");
                    return;
                }

                await ShowModernDialog("출력 완료", $"총 {_collectedDataList.Count}개의 데이터를 성공적으로 엑셀로 내보냈습니다.");
            }
            catch (Exception ex)
            {
                this.WindowState = WindowState.Normal;
                await ShowModernDialog("오류", $"엑셀 출력 중 오류가 발생했습니다.\n{ex.Message}");
            }
            finally
            {
                if (startCell != null && !(startCell is bool)) System.Runtime.InteropServices.Marshal.ReleaseComObject(startCell);
                if (excelApp != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
            }
        }


        private async System.Threading.Tasks.Task ShowModernDialog(string title, string content)
        {
            var msgBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                // 단순 텍스트 대신 TextBlock으로 감싸서 여백과 줄바꿈을 깔끔하게 처리합니다.
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10)
                },
                CloseButtonText = "확인",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,

                // ⭐️ 무식하게 커지지 않도록 크기를 딱 보기 좋게 고정합니다.
                Width = 400,
                Height = 200
            };

            // ⭐️ 가장 중요한 부분: 새로 띄워지는 팝업창에도 현재 앱의 테마를 강제로 적용합니다.
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(msgBox);

            await msgBox.ShowDialogAsync();
        }

    }
}