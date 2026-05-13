using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using Wpf.Ui.Controls;

// 오토캐드 API 참조
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

// NetTopologySuite (SHP 생성 라이브러리)
using NetTopologySuite.Geometries;
using NetTopologySuite.Features;
using NetTopologySuite.IO;

namespace Kys_cad_plugin.Views
{
    public partial class AreaToShpUI : FluentWindow
    {
        public AreaToShpUI()
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

            this.WindowState = WindowState.Minimized; // 객체 선택을 위해 창 숨기기

            try
            {
                // 1. 폴리선(영역)만 선택할 수 있도록 필터 적용
                TypedValue[] tvs = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<OR"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.Start, "POLYLINE"),
                    new TypedValue((int)DxfCode.Operator, "OR>")
                };
                SelectionFilter filter = new SelectionFilter(tvs);

                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\nSHP로 변환할 영역(폴리선)을 선택하세요 (완료 시 Enter): ";

                PromptSelectionResult psr = ed.GetSelection(pso, filter);

                if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
                {
                    this.WindowState = WindowState.Normal;
                    AddLog("객체 선택이 취소되었거나 선택된 객체가 없습니다.");
                    return;
                }

                this.WindowState = WindowState.Normal; // 창 복구

                // 2. 저장할 파일 경로 지정
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "Shapefile (*.shp)|*.shp",
                    FileName = $"AreaExport_{DateTime.Now:yyyyMMdd_HHmm}",
                    Title = "SHP 파일 저장 위치 선택"
                };

                if (saveDialog.ShowDialog() != true)
                {
                    AddLog("파일 저장이 취소되었습니다.");
                    return;
                }

                string shpFilePath = saveDialog.FileName;
                AddLog("선택된 객체 분석 및 SHP 변환 시작...");

                // 3. 캐드 객체를 NTS Feature(도형+속성)로 변환
                var factory = new GeometryFactory();
                var features = new List<Feature>();

                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        int idCounter = 1;

                        foreach (SelectedObject so in psr.Value)
                        {
                            Entity ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;

                            List<Coordinate> coords = new List<Coordinate>();

                            // 2D 폴리선 (LWPOLYLINE) 처리
                            if (ent is Polyline pline)
                            {
                                for (int i = 0; i < pline.NumberOfVertices; i++)
                                {
                                    Point2d pt = pline.GetPoint2dAt(i);
                                    coords.Add(new Coordinate(pt.X, pt.Y));
                                }

                                // 다각형(Polygon)을 만들기 위해서는 시작점과 끝점이 같아야 함(Closed)
                                if (!coords.First().Equals2D(coords.Last()))
                                {
                                    coords.Add(new Coordinate(coords.First().X, coords.First().Y));
                                }
                            }
                            // 3D 폴리선 (POLYLINE) 처리
                            else if (ent is Polyline3d pl3d)
                            {
                                foreach (ObjectId vId in pl3d)
                                {
                                    PolylineVertex3d vtx = tr.GetObject(vId, OpenMode.ForRead) as PolylineVertex3d;
                                    coords.Add(new Coordinate(vtx.Position.X, vtx.Position.Y));
                                }
                                if (!coords.First().Equals2D(coords.Last()))
                                {
                                    coords.Add(new Coordinate(coords.First().X, coords.First().Y));
                                }
                            }

                            // 점이 4개 이상이어야 유효한 면(Polygon) 구성 가능 (삼각형 3개 + 닫힘점 1개)
                            if (coords.Count >= 4)
                            {
                                try
                                {
                                    // Polygon 기하 데이터 생성
                                    var linearRing = factory.CreateLinearRing(coords.ToArray());
                                    var polygon = factory.CreatePolygon(linearRing);

                                    // 속성(Attribute) 데이터 생성 (DBF에 들어갈 정보)
                                    var attributes = new AttributesTable();
                                    attributes.Add("ID", idCounter);
                                    attributes.Add("Layer", ent.Layer); // 도면층 이름 저장

                                    features.Add(new Feature(polygon, attributes));
                                    idCounter++;
                                }
                                catch { /* 꼬인 폴리선 등 기하학적 에러 무시 */ }
                            }
                        }
                        tr.Commit();
                    }
                }

                if (features.Count == 0)
                {
                    await ShowModernDialog("경고", "유효한 닫힌 폴리곤으로 변환할 수 있는 객체가 없습니다.");
                    return;
                }

                // 4. SHP 파일 작성 (ShapefileDataWriter 사용)
                var shapeWriter = new ShapefileDataWriter(shpFilePath)
                {
                    Header = ShapefileDataWriter.GetHeader(features[0], features.Count)
                };

                shapeWriter.Write(features);

                AddLog($"변환 성공! {features.Count}개의 영역이 SHP 파일로 저장되었습니다.");
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