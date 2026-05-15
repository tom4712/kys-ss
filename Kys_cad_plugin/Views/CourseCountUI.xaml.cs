// 오토캐드 API
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Collections.ObjectModel;
using System.Diagnostics; // 프로세스(메모장) 실행을 위해 추가
using System.IO;          // 파일 처리를 위해 추가
using System.Text;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

// 오토캐드 API
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    // 데이터 모델 (객체 ID 리스트 추가)
    public class CourseData
    {
        public string Course { get; set; }
        public int MainCount { get; set; }
        public int ReshootCount { get; set; }
        public int TotalCount => MainCount + ReshootCount;
        public List<ObjectId> ObjectIds { get; set; } = new List<ObjectId>(); // 해당 코스의 객체들을 추적하기 위함
    }

    public partial class CourseCountUI : FluentWindow
    {
        private ObservableCollection<CourseData> _resultList = new ObservableCollection<CourseData>();

        public CourseCountUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
            ResultDataGrid.ItemsSource = _resultList;
        }

        private void CboTargetMode_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ChkOnlyTTLayer != null)
            {
                ChkOnlyTTLayer.IsChecked = (CboTargetMode.SelectedIndex == 1);
            }
        }

        private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            bool isAllOpenDocs = CboTargetMode.SelectedIndex == 1;
            bool onlyTTLayer = ChkOnlyTTLayer.IsChecked == true;

            List<Document> targetDocs = new List<Document>();

            if (isAllOpenDocs)
            {
                foreach (Document doc in CadApp.DocumentManager)
                {
                    targetDocs.Add(doc);
                }
            }
            else
            {
                Document activeDoc = CadApp.DocumentManager.MdiActiveDocument;
                if (activeDoc != null) targetDocs.Add(activeDoc);
            }

            if (targetDocs.Count == 0)
            {
                await ShowModernDialog("알림", "분석할 도면이 없습니다.");
                return;
            }

            // UI 초기화
            BtnAnalyze.IsEnabled = false;
            _resultList.Clear();
            PrgAnalyze.Value = 0;
            TxtProgressPercent.Text = "0%";
            TxtTotalFound.Text = "검색 중...";

            // 텍스트와 ObjectId를 함께 저장할 리스트 튜플 생성
            List<(string Text, ObjectId Id)> rawTextList = new List<(string, ObjectId)>();

            try
            {
                foreach (Document doc in targetDocs)
                {
                    using (DocumentLock loc = doc.LockDocument())
                    {
                        using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                        {
                            BlockTableRecord btr = tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;

                            foreach (ObjectId id in btr)
                            {
                                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                                if (ent == null) continue;

                                if (onlyTTLayer && !ent.Layer.Contains("TT", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                string textVal = null;
                                if (ent is DBText dbText) textVal = dbText.TextString;
                                else if (ent is MText mText) textVal = mText.Text;

                                if (!string.IsNullOrWhiteSpace(textVal))
                                {
                                    rawTextList.Add((textVal.Trim(), id));
                                }
                            }
                            tr.Commit();
                        }
                    }
                }

                int totalTexts = rawTextList.Count;
                TxtTotalFound.Text = totalTexts.ToString("N0");

                if (totalTexts == 0)
                {
                    await ShowModernDialog("알림", "조건에 맞는 텍스트(물량)를 찾을 수 없습니다.");
                    BtnAnalyze.IsEnabled = true;
                    return;
                }

                Dictionary<string, CourseData> courseDict = new Dictionary<string, CourseData>();

                await Task.Run(() =>
                {
                    int processedCount = 0;

                    foreach (var item in rawTextList)
                    {
                        string[] parts = item.Text.Split('_');

                        if (parts.Length >= 2)
                        {
                            string course = parts[0].TrimStart('0');
                            if (string.IsNullOrEmpty(course)) course = "0";

                            string photoNum = parts[1];

                            if (!courseDict.ContainsKey(course))
                            {
                                courseDict[course] = new CourseData { Course = course };
                            }

                            char lastChar = photoNum.Last();
                            bool isReshoot = char.IsLetter(lastChar);

                            if (isReshoot) courseDict[course].ReshootCount++;
                            else courseDict[course].MainCount++;

                            // 객체 ID 저장 (나중에 더블클릭 시 찾아가기 위함)
                            courseDict[course].ObjectIds.Add(item.Id);
                        }

                        processedCount++;

                        if (processedCount % 100 == 0 || processedCount == totalTexts)
                        {
                            int percent = (int)((double)processedCount / totalTexts * 100);

                            Dispatcher.Invoke(() =>
                            {
                                PrgAnalyze.Value = percent;
                                TxtProgressPercent.Text = $"{percent}%";
                            });
                        }
                    }
                });

                var sortedList = courseDict.Values.OrderBy(c =>
                {
                    int num;
                    return int.TryParse(c.Course, out num) ? num : int.MaxValue;
                }).ToList();

                foreach (var item in sortedList)
                {
                    _resultList.Add(item);
                }

                if (sortedList.Count > 0)
                {
                    _resultList.Add(new CourseData
                    {
                        Course = "[총 합계]",
                        MainCount = sortedList.Sum(x => x.MainCount),
                        ReshootCount = sortedList.Sum(x => x.ReshootCount)
                    });
                }
            }
            catch (Exception ex)
            {
                await ShowModernDialog("오류", $"분석 중 오류 발생: {ex.Message}");
            }
            finally
            {
                BtnAnalyze.IsEnabled = true;
            }
        }

        // [새로 추가된 로직] 데이터그리드 더블클릭 이벤트 (화면 이동 및 객체 선택)
        private async void ResultDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultDataGrid.SelectedItem is CourseData selectedCourse)
            {
                // [총 합계] 행을 눌렀거나 저장된 객체 ID가 없는 경우 패스
                if (selectedCourse.Course == "[총 합계]" || selectedCourse.ObjectIds.Count == 0) return;

                Document doc = CadApp.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                Database db = doc.Database;
                Editor ed = doc.Editor;

                try
                {
                    Extents3d totalExtents = new Extents3d();
                    bool hasExtents = false;
                    List<ObjectId> validIds = new List<ObjectId>();

                    using (DocumentLock docLock = doc.LockDocument())
                    {
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            foreach (ObjectId id in selectedCourse.ObjectIds)
                            {
                                if (id.IsErased) continue;
                                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;

                                if (ent != null && ent.Bounds.HasValue)
                                {
                                    if (!hasExtents)
                                    {
                                        totalExtents = ent.Bounds.Value;
                                        hasExtents = true;
                                    }
                                    else
                                    {
                                        totalExtents.AddExtents(ent.Bounds.Value);
                                    }
                                    validIds.Add(id);
                                }
                            }
                            tr.Commit();
                        }
                    }

                    if (hasExtents)
                    {
                        // 1. 해당 코스의 객체들을 선택(파란 그립 켜짐)
                        ed.SetImpliedSelection(validIds.ToArray());

                        // 2. 화면을 해당 객체들이 모여있는 곳으로 줌(Zoom)
                        ed.UpdateScreen();
                        ViewTableRecord view = ed.GetCurrentView();

                        double width = totalExtents.MaxPoint.X - totalExtents.MinPoint.X;
                        double height = totalExtents.MaxPoint.Y - totalExtents.MinPoint.Y;

                        if (width == 0) width = 100;
                        if (height == 0) height = 100;

                        Point2d center = new Point2d((totalExtents.MaxPoint.X + totalExtents.MinPoint.X) / 2.0,
                                                     (totalExtents.MaxPoint.Y + totalExtents.MinPoint.Y) / 2.0);

                        view.CenterPoint = center;
                        view.Width = width * 1.5;
                        view.Height = height * 1.5;
                        ed.SetCurrentView(view);

                        // 3. 플러그인 창을 내리고 캐드 화면을 보여줌
                        this.WindowState = WindowState.Minimized;
                    }
                }
                catch (Exception ex)
                {
                    await ShowModernDialog("이동 오류", $"화면 이동 중 오류 발생: {ex.Message}");
                }
            }
        }

        // 메서드명은 유지하되, 클립보드 대신 Txt 파일 생성 및 실행으로 변경
        private async void BtnCopyToExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_resultList.Count == 0)
            {
                await ShowModernDialog("알림", "출력할 결과가 없습니다. 먼저 분석을 실행해주세요.");
                return;
            }

            try
            {
                // 1. 임시 파일 경로 설정 (시스템의 Temp 폴더 사용)
                string tempFolder = Path.GetTempPath();
                string fileName = $"CourseAnalysis_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = Path.Combine(tempFolder, fileName);

                // 2. 데이터 구성 (StringBuilder 사용)
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("코스(Course)\t본촬영\t재촬영\t전체매수");

                foreach (var data in _resultList)
                {
                    sb.AppendLine($"{data.Course}\t{data.MainCount}\t{data.ReshootCount}\t{data.TotalCount}");
                }

                // 3. 파일 쓰기 (UTF8 인코딩으로 저장하여 한글 깨짐 방지)
                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

                // 4. 파일 실행 (시스템 기본 텍스트 편집기인 메모장 등으로 열기)
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true // 운영체제 쉘을 사용하여 기본 프로그램 실행
                };
                Process.Start(psi);
            }
            catch (IOException ioEx)
            {
                await ShowModernDialog("파일 오류", $"파일을 생성하거나 쓰는 중 오류가 발생했습니다: {ioEx.Message}");
            }
            catch (Exception ex)
            {
                await ShowModernDialog("오류", $"작업 중 예외가 발생했습니다: {ex.Message}");
            }
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