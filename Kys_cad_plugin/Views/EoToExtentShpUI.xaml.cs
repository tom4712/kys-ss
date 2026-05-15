<<<<<<< HEAD
﻿using Kys_cad_plugin.Core; // ★ 중앙 데이터 매니저 참조 추가
using Microsoft.Win32;
=======
﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Wpf.Ui.Controls;
using Kys_cad_plugin.Core; // ★ 중앙 데이터 매니저 참조 추가

>>>>>>> 7e9172bbb8a61170a8c0f9989deb1cdf1e142fdd
// GIS 라이브러리
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Buffer;
using NetTopologySuite.Operation.Union;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Controls;
// 이름 충돌 방지용 Alias
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using NtsPolygon = NetTopologySuite.Geometries.Polygon;
using WpfLineSegment = System.Windows.Media.LineSegment;

namespace Kys_cad_plugin.Views
{
    public class EoExtentRecord
    {
        public string Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Omega { get; set; } // ★ 7컬럼 대응을 위한 추가
        public double Phi { get; set; }   // ★ 7컬럼 대응을 위한 추가
        public double Kappa { get; set; }
    }

    public partial class EoToExtentShpUI : FluentWindow
    {
        private ObservableCollection<EoExtentRecord> _eoList = new ObservableCollection<EoExtentRecord>();

        private NtsGeometry _baseMergedGeometry = null;
        private NtsGeometry _bufferedGeometry = null;

        private GeometryFactory _factory = new GeometryFactory();

        // 캔버스 드래그 이동(Pan) 제어용 변수
        private bool _isPanning = false;
        private System.Windows.Point _startPanPosition;

        public EoToExtentShpUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
            EoDataGrid.ItemsSource = _eoList;
            CboSensorType.SelectedIndex = 0;
        }

        private void CboSensorType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (TxtWidth == null) return;
            string sel = (CboSensorType.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString();
            switch (sel)
            {
                case "DMC1": SetSensor("7680", "13824", "0.12", "12", true); break;
                case "DMC2": SetSensor("14144", "15552", "0.092", "5.6", true); break;
                case "DMC3": SetSensor("14592", "25728", "0.092", "3.9", true); break;
                case "Osprey4.1": SetSensor("14016", "20544", "0.08", "3.76", true); break;
                case "CountryMapper": SetSensor("31520", "13440", "0.107", "3.76", true); break;
                case "CountryMapper(90)": SetSensor("13440", "31520", "0.107", "3.76", true); break;
                case "사용자입력": SetSensor("", "", "", "", false); break;
            }
        }

        private void SetSensor(string w, string h, string f, string p, bool ro)
        {
            TxtWidth.Text = w; TxtHeight.Text = h; TxtFocal.Text = f; TxtPixelSize.Text = p;
            TxtWidth.IsReadOnly = TxtHeight.IsReadOnly = TxtFocal.IsReadOnly = TxtPixelSize.IsReadOnly = ro;
        }

        // ★ [수정된 로직] 중앙 임포트 매니저 연동 및 외곽선(Union) 계산
        private async void BtnLoadEo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 센서 제원 유효성 확인
                if (!double.TryParse(TxtFocal.Text, out double focal) || !double.TryParse(TxtPixelSize.Text, out double pxSizeMicron))
                {
                    await ShowModernDialog("입력 오류", "초점거리와 픽셀크기 값을 확인해주세요.");
                    return;
                }

                if (!int.TryParse(TxtWidth.Text, out int wPx) || !int.TryParse(TxtHeight.Text, out int hPx))
                {
                    await ShowModernDialog("입력 오류", "이미지 해상도(픽셀) 값을 확인해주세요.");
                    return;
                }

                // 1. 필요한 7개 필드 정의 (통일성 유지)
                var targetFields = new List<string> { "ID", "X", "Y", "Z", "Omega", "Phi", "Kappa" };

                // 2. 중앙 매니저 호출 (파일 로드 및 매핑 팝업)
                var result = await DataImportManager.ImportAndMap(this, targetFields);

                if (result == null || result.Rows.Count == 0) return;

                // 3. UI 및 상태 초기화
                double pxSizeM = pxSizeMicron / 1000000.0;
                _eoList.Clear();
                _baseMergedGeometry = null;
                _bufferedGeometry = null;
                PreviewCanvas.Children.Clear();
                PrgStatus.Value = 0;
                TxtStatus.Text = "영역 분석 중...";

                List<NtsGeometry> allPolygons = new List<NtsGeometry>();
                int totalRows = result.Rows.Count;

                // 4. 비동기 영역 계산 및 병합(Union) 처리
<<<<<<< HEAD
                await Task.Run(() =>
                {
=======
                await Task.Run(() => {
>>>>>>> 7e9172bbb8a61170a8c0f9989deb1cdf1e142fdd
                    int idx = 0;
                    foreach (var row in result.Rows)
                    {
                        try
                        {
                            var eo = new EoExtentRecord
                            {
                                Id = row["ID"],
                                X = double.Parse(row["X"]),
                                Y = double.Parse(row["Y"]),
                                Z = double.Parse(row["Z"]),
                                Omega = double.Parse(row["Omega"]),
                                Phi = double.Parse(row["Phi"]),
                                Kappa = double.Parse(row["Kappa"])
                            };

                            // 개별 영상 영역(Footprint) 계산
                            double gsd = (eo.Z / focal) * pxSizeM;
                            double kRad = eo.Kappa * (Math.PI / 180.0);
                            double hW = (wPx * gsd) / 2.0;
                            double hH = (hPx * gsd) / 2.0;

                            Coordinate tl = Rotate(eo.X, eo.Y, -hW, hH, kRad);
                            Coordinate tr = Rotate(eo.X, eo.Y, hW, hH, kRad);
                            Coordinate br = Rotate(eo.X, eo.Y, hW, -hH, kRad);
                            Coordinate bl = Rotate(eo.X, eo.Y, -hW, -hH, kRad);

                            var ring = _factory.CreateLinearRing(new[] { tl, tr, br, bl, tl });
                            allPolygons.Add(_factory.CreatePolygon(ring));

<<<<<<< HEAD
                            Dispatcher.Invoke(() =>
                            {
=======
                            Dispatcher.Invoke(() => {
>>>>>>> 7e9172bbb8a61170a8c0f9989deb1cdf1e142fdd
                                _eoList.Add(eo);
                                idx++;
                                if (idx % 20 == 0 || idx == totalRows)
                                {
                                    PrgStatus.Value = (double)idx / totalRows * 100;
                                    TxtStatus.Text = $"{idx} / {totalRows} 스캔 중...";
                                }
                            });
                        }
                        catch { idx++; }
                    }

                    // 모든 영역을 하나로 병합 (CascadedUnion)
                    if (allPolygons.Count > 0)
                    {
                        Dispatcher.Invoke(() => TxtStatus.Text = "영역 병합(Union) 계산 중...");
                        _baseMergedGeometry = CascadedPolygonUnion.Union(allPolygons);
                    }
                });

                // 병합 결과에 버퍼 적용 및 화면 그리기
                ApplyBufferAndDraw();

                await ShowModernDialog("분석 완료", $"총 {_eoList.Count}개의 EO 영역이 하나의 외곽선으로 병합되었습니다.");
            }
            catch (Exception ex)
            {
                await ShowModernDialog("오류", $"파일 처리 중 오류 발생: {ex.Message}");
            }
        }

        private Coordinate Rotate(double cx, double cy, double lx, double ly, double rad)
        {
            double rx = lx * Math.Cos(rad) - ly * Math.Sin(rad);
            double ry = lx * Math.Sin(rad) + ly * Math.Cos(rad);
            return new Coordinate(cx + rx, cy + ry);
        }

        private void BtnApplyBuffer_Click(object sender, RoutedEventArgs e)
        {
            ApplyBufferAndDraw();
        }

        private void ApplyBufferAndDraw()
        {
            if (_baseMergedGeometry == null) return;

            if (!double.TryParse(TxtBuffer.Text, out double bufferDist)) bufferDist = 0;

            if (bufferDist == 0)
            {
                _bufferedGeometry = _baseMergedGeometry;
            }
            else
            {
                var bufferParams = new BufferParameters { JoinStyle = JoinStyle.Mitre };
                _bufferedGeometry = _baseMergedGeometry.Buffer(bufferDist, bufferParams);
            }

            // 면적 계산 (km²)
            double areaSqKm = _bufferedGeometry.Area / 1000000.0;
            TxtAreaValue.Text = areaSqKm.ToString("N4");

            DrawGeometryToCanvas();
        }

        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawGeometryToCanvas();
        }

        private void DrawGeometryToCanvas()
        {
            if (_bufferedGeometry == null || _bufferedGeometry.IsEmpty || PreviewCanvas.ActualWidth == 0) return;

            // 캔버스 초기화
            CanvasScale.ScaleX = 1;
            CanvasScale.ScaleY = 1;
            CanvasTranslate.X = 0;
            CanvasTranslate.Y = 0;

            PreviewCanvas.Children.Clear();

            var env = _bufferedGeometry.EnvelopeInternal;

            double scaleX = PreviewCanvas.ActualWidth / env.Width;
            double scaleY = PreviewCanvas.ActualHeight / env.Height;
            double scale = Math.Min(scaleX, scaleY) * 0.9;

            double cx = env.Centre.X;
            double cy = env.Centre.Y;
            double canvasCx = PreviewCanvas.ActualWidth / 2;
            double canvasCy = PreviewCanvas.ActualHeight / 2;

            // 버퍼가 적용된 바깥쪽 영역 그리기
            if (!ReferenceEquals(_baseMergedGeometry, _bufferedGeometry))
            {
                DrawSingleGeometryToCanvas(_bufferedGeometry,
                                           Color.FromRgb(243, 156, 18),
                                           Color.FromArgb(80, 241, 196, 15),
                                           cx, cy, scale, canvasCx, canvasCy);
            }

            // 원본 병합 영역 그리기
            if (_baseMergedGeometry != null && !_baseMergedGeometry.IsEmpty)
            {
                DrawSingleGeometryToCanvas(_baseMergedGeometry,
                                           Color.FromRgb(46, 204, 113),
                                           Color.FromArgb(120, 39, 174, 96),
                                           cx, cy, scale, canvasCx, canvasCy);
            }
        }

        private void DrawSingleGeometryToCanvas(NtsGeometry geomToDraw, Color strokeColor, Color fillColor, double cx, double cy, double scale, double canvasCx, double canvasCy)
        {
            System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
            path.Stroke = new SolidColorBrush(strokeColor);
            path.StrokeThickness = 1.5;
            path.Fill = new SolidColorBrush(fillColor);

            PathGeometry pg = new PathGeometry();

            for (int i = 0; i < geomToDraw.NumGeometries; i++)
            {
                var geom = geomToDraw.GetGeometryN(i) as NtsPolygon;
                if (geom != null)
                {
                    PathFigure exterior = CreatePathFigure(geom.ExteriorRing, cx, cy, scale, canvasCx, canvasCy);
                    pg.Figures.Add(exterior);

                    foreach (var hole in geom.InteriorRings)
                    {
                        PathFigure interior = CreatePathFigure(hole, cx, cy, scale, canvasCx, canvasCy);
                        pg.Figures.Add(interior);
                    }
                }
            }

            path.Data = pg;
            PreviewCanvas.Children.Add(path);
        }

        private PathFigure CreatePathFigure(LineString ring, double cx, double cy, double scale, double canvasCx, double canvasCy)
        {
            PathFigure pf = new PathFigure();
            pf.StartPoint = MapPoint(ring.GetCoordinateN(0), cx, cy, scale, canvasCx, canvasCy);

            for (int j = 1; j < ring.NumPoints; j++)
            {
                pf.Segments.Add(new WpfLineSegment(MapPoint(ring.GetCoordinateN(j), cx, cy, scale, canvasCx, canvasCy), true));
            }
            pf.IsClosed = true;
            return pf;
        }

        private System.Windows.Point MapPoint(Coordinate c, double cx, double cy, double scale, double canvasCx, double canvasCy)
        {
            double x = (c.X - cx) * scale + canvasCx;
            double y = -(c.Y - cy) * scale + canvasCy;
            return new System.Windows.Point(x, y);
        }

        // 마우스 휠 줌 처리
        private void PreviewBorder_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.2 : (1.0 / 1.2);
            System.Windows.Point mousePos = e.GetPosition(PreviewCanvas);

            CanvasScale.ScaleX *= zoomFactor;
            CanvasScale.ScaleY *= zoomFactor;

            if (CanvasScale.ScaleX < 0.1) CanvasScale.ScaleX = 0.1;
            if (CanvasScale.ScaleX > 50) CanvasScale.ScaleX = 50;

            CanvasTranslate.X -= mousePos.X * (CanvasScale.ScaleX - CanvasScale.ScaleX / zoomFactor);
            CanvasTranslate.Y -= mousePos.Y * (CanvasScale.ScaleY - CanvasScale.ScaleY / zoomFactor);
        }

        // 마우스 드래그 팬(Pan) 처리
        private void PreviewBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isPanning = true;
            _startPanPosition = e.GetPosition(PreviewBorder);
            PreviewBorder.CaptureMouse();
        }

        private void PreviewBorder_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isPanning)
            {
                System.Windows.Point currentPos = e.GetPosition(PreviewBorder);
                System.Windows.Vector delta = currentPos - _startPanPosition;
                CanvasTranslate.X += delta.X;
                CanvasTranslate.Y += delta.Y;
                _startPanPosition = currentPos;
            }
        }

        private void PreviewBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isPanning) { _isPanning = false; PreviewBorder.ReleaseMouseCapture(); }
        }

        private void PreviewBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isPanning) { _isPanning = false; PreviewBorder.ReleaseMouseCapture(); }
        }

        // SHP 파일 출력
        private async void BtnExportShp_Click(object sender, RoutedEventArgs e)
        {
            if (_bufferedGeometry == null)
            {
                await ShowModernDialog("알림", "저장할 데이터가 없습니다. 먼저 EO 파일을 불러오세요.");
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog { Filter = "Shapefile (*.shp)|*.shp", FileName = $"TotalExtent_{DateTime.Now:yyyyMMdd}" };
            if (sfd.ShowDialog() != true) return;

            try
            {
                var features = new List<Feature>();
                double areaSqKm = _bufferedGeometry.Area / 1000000.0;

                var attr = new AttributesTable();
                attr.Add("ID", "Project_Extent");
                attr.Add("Area_sqkm", areaSqKm);

                features.Add(new Feature(_bufferedGeometry, attr));

                var writer = new ShapefileDataWriter(sfd.FileName) { Header = ShapefileDataWriter.GetHeader(features[0], features.Count) };
                writer.Write(features);

                await ShowModernDialog("성공", $"전체 영역 SHP 파일이 성공적으로 생성되었습니다.\n면적: {areaSqKm:N4} km²\n경로: {sfd.FileName}");
            }
            catch (Exception ex)
            {
                await ShowModernDialog("저장 오류", $"SHP 생성 중 오류 발생: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ShowModernDialog(string title, string content)
        {
            var m = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = new System.Windows.Controls.TextBlock { Text = content, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(10) },
                CloseButtonText = "확인",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 400,
                Height = 200
            };
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(m);
            await m.ShowDialogAsync();
        }
    }
}