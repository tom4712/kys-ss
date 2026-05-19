using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Microsoft.Win32;
using NetTopologySuite.Geometries; // NTS 공간 데이터
using NetTopologySuite.IO;         // NTS SHP 리더
using System;
using System.IO;
using System.Windows;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;
// Point 충돌 방지를 위한 Alias 지정
using NtsPoint = NetTopologySuite.Geometries.Point;

namespace Kys_cad_plugin.Views
{
    public partial class ShpImportUI : FluentWindow
    {
        public ShpImportUI()
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

        private async System.Threading.Tasks.Task ShowModernDialog(string title, string content)
        {
            var msgBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = new System.Windows.Controls.TextBlock { Text = content, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10) },
                CloseButtonText = "확인",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(msgBox);
            await msgBox.ShowDialogAsync();
        }

        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Shapefile (*.shp)|*.shp",
                Title = "불러올 SHP 파일 선택"
            };

            if (ofd.ShowDialog() == true)
            {
                TxtFilePath.Text = ofd.FileName;
                AddLog($"파일 선택됨: {Path.GetFileName(ofd.FileName)}");
            }
        }

        private async void BtnImportShp_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtFilePath.Text) || !File.Exists(TxtFilePath.Text))
            {
                await ShowModernDialog("알림", "먼저 불러올 SHP 파일을 선택해주세요.");
                return;
            }

            string targetLayerName = string.IsNullOrWhiteSpace(TxtLayerName.Text) ? "SHP_IMPORT_DATA" : TxtLayerName.Text.Trim();

            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            int successCount = 0;

            try
            {
                AddLog("SHP 데이터 분석 및 도면 작도 시작...");

                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                        // 도면층 생성 또는 가져오기
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
                                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3) // 초록색 기본
                            };
                            layerId = lt.Add(ltr);
                            tr.AddNewlyCreatedDBObject(ltr, true);
                        }

                        // ★ NetTopologySuite를 이용한 SHP 읽기
                        using (var reader = new ShapefileDataReader(TxtFilePath.Text, new GeometryFactory()))
                        {
                            while (reader.Read())
                            {
                                var geom = reader.Geometry;
                                if (geom == null) continue;

                                // 기하학적 형태에 따른 캐드 객체 생성 분기
                                DrawGeometryToCad(geom, btr, tr, layerId);
                                successCount++;
                            }
                        }

                        tr.Commit();
                    }
                }

                ed.UpdateScreen();
                AddLog($"작도 완료! 총 {successCount}개의 객체가 도면에 생성되었습니다.");
                await ShowModernDialog("성공", $"SHP 데이터 {successCount}개를 성공적으로 도면에 그렸습니다!");
            }
            catch (Exception ex)
            {
                AddLog($"오류 발생: {ex.Message}", true);
                await ShowModernDialog("오류", $"SHP 불러오기 중 오류가 발생했습니다:\n{ex.Message}");
            }
        }

        // 지오메트리 타입별 분기 처리 헬퍼 함수
        private void DrawGeometryToCad(Geometry geom, BlockTableRecord btr, Transaction tr, ObjectId layerId)
        {
            if (geom is Polygon polygon)
            {
                // 다각형의 외곽선(Exterior) 그리기
                DrawLinearRing(polygon.ExteriorRing, btr, tr, layerId, true);

                // 다각형 내부의 뚫린 구멍(Interior) 그리기
                foreach (var hole in polygon.InteriorRings)
                {
                    DrawLinearRing(hole, btr, tr, layerId, true);
                }
            }
            else if (geom is MultiPolygon multiPolygon)
            {
                foreach (var p in multiPolygon.Geometries)
                {
                    DrawGeometryToCad(p, btr, tr, layerId); // 재귀 호출
                }
            }
            else if (geom is LineString lineString)
            {
                // 열린 선 그리기
                DrawLinearRing(lineString, btr, tr, layerId, false);
            }
            else if (geom is MultiLineString multiLineString)
            {
                foreach (var ls in multiLineString.Geometries)
                {
                    DrawGeometryToCad(ls, btr, tr, layerId);
                }
            }
            else if (geom is NtsPoint pt)
            {
                // 캐드 Point 객체 생성
                DBPoint dbp = new DBPoint(new Point3d(pt.X, pt.Y, 0)) { LayerId = layerId };
                btr.AppendEntity(dbp);
                tr.AddNewlyCreatedDBObject(dbp, true);
            }
            else if (geom is MultiPoint multiPoint)
            {
                foreach (var p in multiPoint.Geometries)
                {
                    DrawGeometryToCad(p, btr, tr, layerId);
                }
            }
        }

        // 폴리라인을 그려주는 헬퍼 함수
        private void DrawLinearRing(LineString ls, BlockTableRecord btr, Transaction tr, ObjectId layerId, bool isClosed)
        {
            if (ls == null || ls.Coordinates.Length < 2) return;

            Polyline pline = new Polyline();
            pline.LayerId = layerId;

            int vertexIndex = 0;
            for (int i = 0; i < ls.Coordinates.Length; i++)
            {
                var coord = ls.Coordinates[i];

                // 닫힌(Closed) 도형일 경우 NTS는 첫 점과 끝 점을 동일하게 내뱉으므로 마지막 중복 점 생략
                if (isClosed && i == ls.Coordinates.Length - 1 && coord.Equals2D(ls.Coordinates[0]))
                {
                    continue;
                }

                pline.AddVertexAt(vertexIndex, new Point2d(coord.X, coord.Y), 0, 0, 0);
                vertexIndex++;
            }

            pline.Closed = isClosed;

            btr.AppendEntity(pline);
            tr.AddNewlyCreatedDBObject(pline, true);
        }
    }
}