using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    public partial class RetakeAnalysisUI : FluentWindow
    {
        public ObservableCollection<RetakeCourseGroup> CourseGroups { get; set; } = new ObservableCollection<RetakeCourseGroup>();

        public RetakeAnalysisUI()
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

        private async void BtnLoadTexts_Click(object sender, RoutedEventArgs e)
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;

            try
            {
                CourseGroups.Clear();
                var tempData = new Dictionary<string, List<CadTextInfo>>();

                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                        var targetLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (ObjectId lId in lt)
                        {
                            LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lId, OpenMode.ForRead);
                            if (ltr.Name.ToUpper().Contains("TT"))
                            {
                                targetLayers.Add(ltr.Name);
                            }
                        }

                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                        foreach (ObjectId entId in btr)
                        {
                            Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;

                            if (targetLayers.Contains(ent.Layer) && ent is DBText dbText)
                            {
                                string textVal = dbText.TextString?.Trim() ?? "";

                                if (!textVal.ToUpper().Contains("A"))
                                {
                                    if (TryParseCourseAndPhoto(textVal, out string courseName, out int photoNum))
                                    {
                                        if (!tempData.ContainsKey(courseName))
                                            tempData[courseName] = new List<CadTextInfo>();

                                        tempData[courseName].Add(new CadTextInfo
                                        {
                                            PhotoNumber = photoNum,
                                            Position = dbText.Position
                                        });
                                    }
                                }
                            }
                        }
                        tr.Commit();
                    }
                }

                if (tempData.Count == 0)
                {
                    await ShowModernDialog("알림", "도면 내에서 조건에 맞는 텍스트 데이터를 찾지 못했습니다.\n(TT 레이어 내의 '코스_번호' 형식 텍스트)");
                    return;
                }

                var sortedCourses = tempData.Keys.OrderBy(k => k).ToList();
                foreach (var course in sortedCourses)
                {
                    var items = tempData[course];
                    int maxNum = items.Max(i => i.PhotoNumber);
                    int minNum = items.Min(i => i.PhotoNumber);

                    CourseGroups.Add(new RetakeCourseGroup
                    {
                        CourseName = course,
                        ExtractedCount = items.Count,
                        StartNumber = minNum, // 도면에 있는 제일 작은 번호를 시작 번호로 세팅
                        EndNumber = maxNum,   // 제일 큰 번호를 끝 번호로 세팅
                        RawTexts = items
                    });
                }

                await ShowModernDialog("완료", $"도면 텍스트를 분석하여 총 {CourseGroups.Count}개의 코스를 식별하고 정렬했습니다.");
            }
            catch (Exception ex)
            {
                await ShowModernDialog("오류", $"데이터 로드 중 오류: {ex.Message}");
            }
        }

        private bool TryParseCourseAndPhoto(string text, out string courseName, out int photoNumber)
        {
            courseName = string.Empty;
            photoNumber = -1;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string[] parts = text.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;

            string lastPart = parts[parts.Length - 1];
            courseName = string.Join("_", parts.Take(parts.Length - 1));

            string digitStr = "";
            foreach (char c in lastPart)
            {
                if (char.IsDigit(c)) digitStr += c;
            }

            if (!string.IsNullOrEmpty(digitStr) && int.TryParse(digitStr, out photoNumber))
                return true;

            return false;
        }

        private void CourseDataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                string clipboardText = Clipboard.GetText();
                if (string.IsNullOrEmpty(clipboardText)) return;

                string[] lines = clipboardText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (lines.Length == 0) return;

                var currentItem = CourseDataGrid.CurrentItem as RetakeCourseGroup;
                if (currentItem == null) return;

                int startRowIdx = CourseGroups.IndexOf(currentItem);
                int targetColumnIdx = CourseDataGrid.CurrentCell.Column.DisplayIndex;

                for (int i = 0; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    int targetRowIdx = startRowIdx + i;
                    if (targetRowIdx >= CourseGroups.Count) break;

                    if (int.TryParse(lines[i].Trim(), out int parsedValue))
                    {
                        if (targetColumnIdx == 3)
                            CourseGroups[targetRowIdx].EndNumber = parsedValue;
                        else if (targetColumnIdx == 2)
                            CourseGroups[targetRowIdx].StartNumber = parsedValue;
                    }
                }
                CourseDataGrid.Items.Refresh();
                e.Handled = true;
            }
        }

        // ★ [대규모 업그레이드] 3. 재촬영 알고리즘 (수학적 공간 예측 탑재)
        private async void BtnRunAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (CourseGroups.Count == 0)
            {
                await ShowModernDialog("알림", "분석할 코스 데이터가 없습니다. 먼저 도면에서 데이터를 가져오세요.");
                return;
            }

            if (!double.TryParse(TxtBoxHeight.Text, out double boxHeight))
            {
                await ShowModernDialog("오류", "사각형 높이는 숫자로 입력해 주세요.");
                return;
            }

            string targetLayerName = string.IsNullOrWhiteSpace(TxtLayerName.Text) ? "KYSQL_RETAKE_BOX" : TxtLayerName.Text.Trim();

            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;

            try
            {
                int totalBoxCount = 0;

                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                        LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                        ObjectId layerId;
                        if (lt.Has(targetLayerName))
                        {
                            layerId = lt[targetLayerName];
                        }
                        else
                        {
                            lt.UpgradeOpen();
                            LayerTableRecord ltr = new LayerTableRecord
                            {
                                Name = targetLayerName,
                                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 2)
                            };
                            layerId = lt.Add(ltr);
                            tr.AddNewlyCreatedDBObject(ltr, true);
                        }

                        foreach (var group in CourseGroups)
                        {
                            if (group.RawTexts == null || group.RawTexts.Count == 0) continue;

                            var existingNumbers = new HashSet<int>(group.RawTexts.Select(t => t.PhotoNumber));
                            var missingNumbers = new List<int>();

                            for (int n = group.StartNumber; n <= group.EndNumber; n++)
                            {
                                if (!existingNumbers.Contains(n))
                                {
                                    missingNumbers.Add(n);
                                }
                            }

                            if (missingNumbers.Count == 0) continue;

                            // -------------------------------------------------------------
                            // ★ [핵심] 사진 간의 평균 거리(Step X) 및 방향 벡터 연산
                            // -------------------------------------------------------------
                            var distinctTexts = group.RawTexts
                                .GroupBy(t => t.PhotoNumber).Select(g => g.First())
                                .OrderBy(t => t.PhotoNumber).ToList();

                            double stepX = 0;
                            double avgY = distinctTexts.Average(t => t.Position.Y); // 박스의 중심 Y축 유지

                            Point3d refPoint = distinctTexts.First().Position;
                            int refNumber = distinctTexts.First().PhotoNumber;

                            if (distinctTexts.Count >= 2)
                            {
                                var first = distinctTexts.First();
                                var last = distinctTexts.Last();
                                // X 변화량 / 번호 변화량 = 1장당 움직이는 X거리 (방향 포함)
                                stepX = (last.Position.X - first.Position.X) / (last.PhotoNumber - first.PhotoNumber);
                            }
                            else
                            {
                                stepX = 100.0; // 데이터가 1개뿐일 때의 기본 폭 설정
                            }
                            // -------------------------------------------------------------

                            // 누락된 번호를 덩어리(구간)로 묶기
                            var gapRanges = new List<Tuple<int, int>>();
                            int rangeStart = missingNumbers[0];
                            int prev = missingNumbers[0];

                            for (int i = 1; i < missingNumbers.Count; i++)
                            {
                                if (missingNumbers[i] == prev + 1)
                                {
                                    prev = missingNumbers[i];
                                }
                                else
                                {
                                    gapRanges.Add(new Tuple<int, int>(rangeStart, prev));
                                    rangeStart = missingNumbers[i];
                                    prev = missingNumbers[i];
                                }
                            }
                            gapRanges.Add(new Tuple<int, int>(rangeStart, prev));

                            // 각 누락 구간별 처리 (예측된 가상 좌표에 박스 그리기)
                            foreach (var gap in gapRanges)
                            {
                                // 버퍼 3장 적용
                                int bufferedStart = gap.Item1 - 3;
                                int bufferedEnd = gap.Item2 + 3;

                                // 사용자가 설정한 범위 내로 클램핑
                                if (bufferedStart < group.StartNumber) bufferedStart = group.StartNumber;
                                if (bufferedEnd > group.EndNumber) bufferedEnd = group.EndNumber;

                                // ★ 기존의 '존재하는 점'을 찾던 방식을 버리고, 수학적으로 위치를 예측(Extrapolation)합니다.
                                double startX = refPoint.X + stepX * (bufferedStart - refNumber);
                                double endX = refPoint.X + stepX * (bufferedEnd - refNumber);

                                // 동->서, 서->동 역방향에 상관없이 정확히 양 끝 좌표를 Min, Max로 할당
                                double minX = Math.Min(startX, endX);
                                double maxX = Math.Max(startX, endX);

                                // 점 하나만 덩그러니 있을 때 박스 너비가 0이 되는 것을 방지
                                if (Math.Abs(maxX - minX) < 1.0)
                                {
                                    minX -= 50.0;
                                    maxX += 50.0;
                                }

                                Polyline retakeBox = new Polyline { LayerId = layerId, ColorIndex = 2 };
                                double halfH = boxHeight / 2.0;

                                retakeBox.AddVertexAt(0, new Point2d(minX, avgY - halfH), 0, 0, 0);
                                retakeBox.AddVertexAt(1, new Point2d(maxX, avgY - halfH), 0, 0, 0);
                                retakeBox.AddVertexAt(2, new Point2d(maxX, avgY + halfH), 0, 0, 0);
                                retakeBox.AddVertexAt(3, new Point2d(minX, avgY + halfH), 0, 0, 0);
                                retakeBox.Closed = true;

                                btr.AppendEntity(retakeBox);
                                tr.AddNewlyCreatedDBObject(retakeBox, true);
                                totalBoxCount++;
                            }
                        }
                        tr.Commit();
                    }
                }

                doc.Editor.UpdateScreen();
                await ShowModernDialog("성공", $"가상 좌표 예측 알고리즘이 적용되었습니다!\n도면층 [{targetLayerName}]에 총 {totalBoxCount}개의 재촬영 박스를 표기했습니다.");
            }
            catch (Exception ex)
            {
                await ShowModernDialog("오류", $"분석 도중 오류가 발생했습니다: {ex.Message}");
            }
        }
    }

    // ==========================================
    // 내부 데이터 모델
    // ==========================================
    public class CadTextInfo
    {
        public int PhotoNumber { get; set; }
        public Point3d Position { get; set; }
    }

    public class RetakeCourseGroup
    {
        public string CourseName { get; set; }
        public int ExtractedCount { get; set; }
        public int StartNumber { get; set; } = 1;
        public int EndNumber { get; set; } = 1;
        public List<CadTextInfo> RawTexts { get; set; } = new List<CadTextInfo>();
    }
}