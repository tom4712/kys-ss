// 오토캐드 API 참조
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Microsoft.Win32;
using NetTopologySuite.Features;
// NetTopologySuite (SHP 생성 라이브러리)
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.Windows;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    public partial class PointToShpUI : FluentWindow
    {
        public PointToShpUI()
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

        private async void BtnConvertShp_Click(object sender, RoutedEventArgs e)
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            this.WindowState = WindowState.Minimized;

            try
            {
                // 1. 원(Circle)과 텍스트(Text, MText)만 선택하도록 필터 적용
                TypedValue[] tvs = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<OR"),
                    new TypedValue((int)DxfCode.Start, "CIRCLE"),
                    new TypedValue((int)DxfCode.Start, "TEXT"),
                    new TypedValue((int)DxfCode.Start, "MTEXT"),
                    new TypedValue((int)DxfCode.Operator, "OR>")
                };
                SelectionFilter filter = new SelectionFilter(tvs);

                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\nSHP로 변환할 원(주점)과 텍스트를 함께 드래그하여 선택하세요: ";

                PromptSelectionResult psr = ed.GetSelection(pso, filter);

                if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
                {
                    this.WindowState = WindowState.Normal;
                    AddLog("객체 선택이 취소되었거나 선택된 객체가 없습니다.");
                    return;
                }

                this.WindowState = WindowState.Normal;

                // 2. 파일 저장 대화상자
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "Shapefile (*.shp)|*.shp",
                    FileName = $"PointExport_{DateTime.Now:yyyyMMdd_HHmm}",
                    Title = "SHP 파일 저장 위치 선택"
                };

                if (saveDialog.ShowDialog() != true)
                {
                    AddLog("파일 저장이 취소되었습니다.");
                    return;
                }

                string shpFilePath = saveDialog.FileName;
                AddLog("선택된 객체 분석 및 텍스트 매핑 시작...");

                var factory = new GeometryFactory();
                var features = new List<Feature>();

                // 텍스트 데이터를 저장할 딕셔너리 (Key: "X_Y" 형태의 소수점 3자리 좌표, Value: 텍스트 내용)
                Dictionary<string, string> textMap = new Dictionary<string, string>();

                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        // [Pass 1] 선택된 객체 중 텍스트 먼저 수집하여 Dictionary에 저장
                        foreach (SelectedObject so in psr.Value)
                        {
                            Entity ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;

                            string textVal = null;
                            Point3d pos = Point3d.Origin;

                            if (ent is DBText dbText)
                            {
                                textVal = dbText.TextString.Trim();
                                pos = dbText.Position;
                            }
                            else if (ent is MText mText)
                            {
                                textVal = mText.Text.Trim();
                                pos = mText.Location;
                            }

                            if (!string.IsNullOrEmpty(textVal))
                            {
                                // 캐드의 부동소수점 오차를 잡기 위해 소수점 3자리까지 반올림하여 키 생성
                                string coordKey = $"{Math.Round(pos.X, 3)}_{Math.Round(pos.Y, 3)}";

                                // 만약 완벽히 같은 좌표에 텍스트가 여러 개 겹쳐있다면 쉼표로 연결
                                if (textMap.ContainsKey(coordKey))
                                {
                                    textMap[coordKey] += $", {textVal}";
                                }
                                else
                                {
                                    textMap.Add(coordKey, textVal);
                                }
                            }
                        }

                        // [Pass 2] 선택된 객체 중 원(Circle)을 찾아 SHP Feature로 변환
                        int idCounter = 1;
                        int matchedTextCount = 0;

                        foreach (SelectedObject so in psr.Value)
                        {
                            Entity ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                            if (ent == null || !(ent is Circle circle)) continue;

                            // NTS Point 생성
                            var point = factory.CreatePoint(new Coordinate(circle.Center.X, circle.Center.Y));

                            // 원의 중심 좌표를 키값으로 텍스트 딕셔너리 검색
                            string circleCoordKey = $"{Math.Round(circle.Center.X, 3)}_{Math.Round(circle.Center.Y, 3)}";
                            string mappedText = "";

                            if (textMap.ContainsKey(circleCoordKey))
                            {
                                mappedText = textMap[circleCoordKey];
                                matchedTextCount++;
                            }

                            // DBF 속성 테이블 정의
                            var attributes = new AttributesTable();
                            attributes.Add("ID", idCounter);
                            attributes.Add("Layer", circle.Layer);
                            attributes.Add("Name", mappedText); // 매핑된 텍스트 값 (없으면 빈칸)

                            features.Add(new Feature(point, attributes));
                            idCounter++;
                        }
                        tr.Commit();
                        AddLog($"텍스트 매핑 완료. (총 {features.Count}개의 포인트 중 {matchedTextCount}개 매핑 성공)");
                    }
                }

                if (features.Count == 0)
                {
                    await ShowModernDialog("경고", "선택된 영역 내에 원(Circle) 객체가 없습니다.");
                    return;
                }

                // 3. SHP 파일 작성
                var shapeWriter = new ShapefileDataWriter(shpFilePath)
                {
                    Header = ShapefileDataWriter.GetHeader(features[0], features.Count)
                };

                shapeWriter.Write(features);

                AddLog($"변환 성공! {features.Count}개의 주점이 SHP 파일로 저장되었습니다.");
                await ShowModernDialog("완료", $"SHP 변환이 성공적으로 완료되었습니다.\n저장 경로: {shpFilePath}");
            }
            catch (Exception ex)
            {
                AddLog($"오류 발생: {ex.Message}", true);
                await ShowModernDialog("오류", $"SHP 생성 중 오류가 발생했습니다: {ex.Message}");
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