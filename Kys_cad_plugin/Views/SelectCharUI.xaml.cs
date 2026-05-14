// 오토캐드 API 참조
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    public partial class SelectCharUI : FluentWindow
    {
        public SelectCharUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
            TxtSearchKeyword.Focus(); // 창이 열릴 때 바로 검색창에 포커스
        }

        private void AddLog(string message, bool isError = false)
        {
            var tb = new System.Windows.Controls.TextBlock { Text = $"▶ {message}", FontSize = 11, Margin = new Thickness(2) };
            if (isError) tb.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
            else tb.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            LogListBox.Items.Add(tb);
            LogListBox.ScrollIntoView(tb);
        }

        // 엔터키를 누르면 바로 검색 실행
        private void TxtSearchKeyword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnSearchAndSelect_Click(sender, null);
            }
        }

        private async void BtnSearchAndSelect_Click(object sender, RoutedEventArgs e)
        {
            string keyword = TxtSearchKeyword.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                await ShowModernDialog("알림", "검색할 문자를 입력해주세요.");
                TxtSearchKeyword.Focus();
                return;
            }

            bool isExactMatch = ChkExactMatch.IsChecked == true;

            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                AddLog($"도면에서 '{keyword}' 문자 검색 시작...");

                List<ObjectId> matchedIds = new List<ObjectId>();
                Extents3d totalExtents = new Extents3d();
                bool hasData = false;

                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        // 현재 도면의 활성 공간(ModelSpace or PaperSpace)을 가져옵니다.
                        BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;

                        foreach (ObjectId objId in btr)
                        {
                            Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;

                            string textContent = null;

                            // Text 또는 MText인지 확인하고 내용 추출
                            if (ent is DBText dbText)
                            {
                                textContent = dbText.TextString;
                            }
                            else if (ent is MText mText)
                            {
                                textContent = mText.Text; // .Contents 는 서식 코드가 포함되므로 순수 텍스트인 .Text 사용
                            }

                            if (!string.IsNullOrEmpty(textContent))
                            {
                                bool isMatch = false;

                                if (isExactMatch)
                                {
                                    isMatch = textContent.Equals(keyword, StringComparison.OrdinalIgnoreCase);
                                }
                                else
                                {
                                    isMatch = textContent.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
                                }

                                // 매칭 성공 시
                                if (isMatch)
                                {
                                    matchedIds.Add(objId);

                                    // 객체의 바운딩 박스를 계산하여 전체 화면 이동 범위를 구함
                                    if (ent.Bounds.HasValue)
                                    {
                                        if (!hasData)
                                        {
                                            totalExtents = ent.Bounds.Value;
                                            hasData = true;
                                        }
                                        else
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

                if (matchedIds.Count > 0)
                {
                    // 1. 찾은 객체들을 캐드 상에서 활성화(선택) 상태로 만들기
                    ed.SetImpliedSelection(matchedIds.ToArray());

                    // 2. 화면 이동 (Zoom to Extents)
                    if (hasData)
                    {
                        ed.UpdateScreen();
                        ViewTableRecord view = ed.GetCurrentView();

                        double width = totalExtents.MaxPoint.X - totalExtents.MinPoint.X;
                        double height = totalExtents.MaxPoint.Y - totalExtents.MinPoint.Y;

                        // 화면 꽉 차게 보이지 않고 살짝 여유를 줌 (1.5배)
                        if (width == 0) width = 10;
                        if (height == 0) height = 10;

                        Point2d center = new Point2d((totalExtents.MaxPoint.X + totalExtents.MinPoint.X) / 2.0,
                                                     (totalExtents.MaxPoint.Y + totalExtents.MinPoint.Y) / 2.0);

                        view.CenterPoint = center;
                        view.Width = width * 1.5;
                        view.Height = height * 1.5;

                        ed.SetCurrentView(view);
                    }

                    AddLog($"검색 완료: 총 {matchedIds.Count}개의 텍스트를 찾아 선택했습니다.");

                    // 선택 완료 후 메인 창을 최소화하여 오토캐드 화면을 보여줌
                    this.WindowState = WindowState.Minimized;
                }
                else
                {
                    // 기존 선택 해제
                    ed.SetImpliedSelection(new ObjectId[0]);
                    AddLog($"'{keyword}' 문자를 포함한 텍스트를 찾을 수 없습니다.");
                    await ShowModernDialog("검색 결과 없음", $"'{keyword}' 문자가 포함된 텍스트가 현재 도면에 존재하지 않습니다.");
                }
            }
            catch (Exception ex)
            {
                AddLog($"오류 발생: {ex.Message}", true);
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