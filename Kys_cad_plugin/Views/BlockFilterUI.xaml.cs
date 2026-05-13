using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    public partial class BlockFilterUI : FluentWindow
    {
        // 블럭(BL) 이름을 Key로, 해당 블럭의 코스 번호들을 Value로 가지는 딕셔너리
        private Dictionary<string, HashSet<string>> _blockCourseMap = new Dictionary<string, HashSet<string>>();

        public BlockFilterUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
        }

        private void AddLog(string message, bool isError = false)
        {
            var tb = new System.Windows.Controls.TextBlock { Text = $"▶ {message}", FontSize = 11, Margin = new Thickness(2) };
            if (isError) tb.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
            else tb.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            LogListBox.Items.Add(tb);
            LogListBox.ScrollIntoView(tb);
        }

        // DB 텍스트 파일 불러오기
        private async void BtnLoadDb_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "텍스트/CSV 파일 (*.txt;*.csv)|*.txt;*.csv|모든 파일 (*.*)|*.*",
                Title = "블럭 DB 파일 선택"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _blockCourseMap.Clear();
                    CboBlockList.Items.Clear();
                    TxtDbFilePath.Text = openFileDialog.FileName;

                    using (FileStream fs = new FileStream(openFileDialog.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader sr = new StreamReader(fs, Encoding.Default))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            // 탭이나 공백으로 분리
                            string[] parts = line.Split(new char[] { '\t', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                            if (parts.Length >= 2)
                            {
                                string blockName = parts[0].Trim();
                                string courseNum = parts[1].Trim();

                                if (!_blockCourseMap.ContainsKey(blockName))
                                {
                                    _blockCourseMap[blockName] = new HashSet<string>();
                                }
                                _blockCourseMap[blockName].Add(courseNum);
                            }
                        }
                    }

                    // 콤보박스에 정렬된 블럭 이름 추가
                    var sortedBlocks = _blockCourseMap.Keys.OrderBy(b =>
                    {
                        // "1BL", "10BL" 정렬을 위해 앞의 숫자만 추출하여 정렬
                        string numPart = new string(b.TakeWhile(char.IsDigit).ToArray());
                        return int.TryParse(numPart, out int num) ? num : int.MaxValue;
                    }).ToList();

                    foreach (var block in sortedBlocks)
                    {
                        CboBlockList.Items.Add(block);
                    }

                    if (CboBlockList.Items.Count > 0) CboBlockList.SelectedIndex = 0;

                    AddLog($"{openFileDialog.SafeFileName} 로드 완료. (총 {_blockCourseMap.Keys.Count}개의 블럭 발견)");
                }
                catch (Exception ex)
                {
                    await ShowModernDialog("파일 읽기 오류", ex.Message);
                }
            }
        }

        // 객체 찾기 및 화면 이동
        private async void BtnFindAndMove_Click(object sender, RoutedEventArgs e)
        {
            if (CboBlockList.SelectedItem == null || string.IsNullOrEmpty(CboBlockList.SelectedItem.ToString()))
            {
                await ShowModernDialog("알림", "검색할 블럭(BL)을 선택해주세요.");
                return;
            }

            string selectedBlock = CboBlockList.SelectedItem.ToString();
            HashSet<string> targetCourses = _blockCourseMap[selectedBlock];

            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                AddLog($"도면 스캔 중... (대상: {selectedBlock}, 코스 {targetCourses.Count}개)");

                List<ObjectId> idsToSelect = new List<ObjectId>();
                HashSet<string> validCoordinates = new HashSet<string>();
                Extents3d totalExtents = new Extents3d();
                bool hasExtents = false;

                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;

                        // [Pass 1] TT 도면층에서 기준이 되는 텍스트 좌표 수집
                        foreach (ObjectId id in btr)
                        {
                            Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent == null || !ent.Layer.Contains("TT", StringComparison.OrdinalIgnoreCase)) continue;

                            string textVal = null;
                            Point3d textPos = Point3d.Origin;

                            if (ent is DBText dbText)
                            {
                                textVal = dbText.TextString.Trim();
                                textPos = dbText.Position;
                            }
                            else if (ent is MText mText)
                            {
                                textVal = mText.Text.Trim();
                                textPos = mText.Location;
                            }

                            if (!string.IsNullOrEmpty(textVal))
                            {
                                string[] parts = textVal.Split('_');
                                if (parts.Length >= 2)
                                {
                                    string courseNum = parts[0].TrimStart('0');
                                    if (string.IsNullOrEmpty(courseNum)) courseNum = "0";

                                    if (targetCourses.Contains(courseNum))
                                    {
                                        idsToSelect.Add(id);

                                        string coordKey = $"{Math.Round(textPos.X, 3)}_{Math.Round(textPos.Y, 3)}";
                                        validCoordinates.Add(coordKey);

                                        if (ent.Bounds.HasValue)
                                        {
                                            if (!hasExtents) { totalExtents = ent.Bounds.Value; hasExtents = true; }
                                            else totalExtents.AddExtents(ent.Bounds.Value);
                                        }
                                    }
                                }
                            }
                        }

                        // [Pass 2] 수집된 좌표를 바탕으로 TT 또는 PP 도면층의 원(Circle) 선택
                        if (validCoordinates.Count > 0)
                        {
                            foreach (ObjectId id in btr)
                            {
                                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                                if (ent == null) continue;

                                // 레이어 이름에 "TT" 또는 "PP"가 포함되어 있는지 확인
                                bool isTargetLayer = ent.Layer.Contains("TT", StringComparison.OrdinalIgnoreCase) ||
                                                   ent.Layer.Contains("PP", StringComparison.OrdinalIgnoreCase);

                                if (isTargetLayer && ent is Circle circle)
                                {
                                    string coordKey = $"{Math.Round(circle.Center.X, 3)}_{Math.Round(circle.Center.Y, 3)}";

                                    if (validCoordinates.Contains(coordKey))
                                    {
                                        idsToSelect.Add(id);

                                        if (ent.Bounds.HasValue)
                                        {
                                            totalExtents.AddExtents(ent.Bounds.Value);
                                        }
                                    }
                                }
                            }
                        }
                        tr.Commit();
                    }
                }

                if (idsToSelect.Count > 0)
                {
                    ed.SetImpliedSelection(idsToSelect.ToArray());

                    if (hasExtents)
                    {
                        ed.UpdateScreen();
                        ViewTableRecord view = ed.GetCurrentView();

                        double width = totalExtents.MaxPoint.X - totalExtents.MinPoint.X;
                        double height = totalExtents.MaxPoint.Y - totalExtents.MinPoint.Y;

                        if (width == 0) width = 100;
                        if (height == 0) height = 100;

                        Point2d center = new Point2d((totalExtents.MaxPoint.X + totalExtents.MinPoint.X) / 2.0,
                                                     (totalExtents.MaxPoint.Y + totalExtents.MinPoint.Y) / 2.0);

                        view.CenterPoint = center;
                        view.Width = width * 1.3;
                        view.Height = height * 1.3;
                        ed.SetCurrentView(view);
                    }

                    AddLog($"분석 완료. [TT/PP] 도면층의 텍스트와 원 총 {idsToSelect.Count}개가 선택되었습니다.");
                    this.WindowState = WindowState.Minimized;
                }
                else
                {
                    ed.SetImpliedSelection(new ObjectId[0]);
                    await ShowModernDialog("검색 결과 없음", $"현재 도면에서 '{selectedBlock}' 블럭에 해당하는 객체를 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                await ShowModernDialog("오류", $"검색 중 오류 발생: {ex.Message}");
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