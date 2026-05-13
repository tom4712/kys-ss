using Microsoft.Win32;
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

// GIS 라이브러리
using NetTopologySuite.Features;
using NetTopologySuite.IO;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Operation.Buffer;
using NetTopologySuite.Geometries;

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
        public double Kappa { get; set; }
    }

    public partial class EoToExtentShpUI : FluentWindow
    {
        private ObservableCollection<EoExtentRecord> _eoList = new ObservableCollection<EoExtentRecord>();

        private NtsGeometry _baseMergedGeometry = null;
        private NtsGeometry _bufferedGeometry = null;

        private GeometryFactory _factory = new GeometryFactory();

        // 캔버스 드래그 이동(Pan) 제어용 변수
        // ★ NTS의 Point와 충돌하지 않도록 System.Windows.Point로 명시
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

        private async void BtnLoadEo_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "텍스트 파일 (*.txt;*.csv)|*.txt;*.csv" };
            if (ofd.ShowDialog() != true) return;

            try
            {
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

                double pxSizeM = pxSizeMicron / 1000000.0;
                _eoList.Clear();
                _baseMergedGeometry = null;
                _bufferedGeometry = null;
                PreviewCanvas.Children.Clear();

                string[] lines = File.ReadAllLines(ofd.FileName, Encoding.Default);
                List<NtsGeometry> allPolygons = new List<NtsGeometry>();

                await Task.Run(() => {
                    int idx = 0;
                    foreach (string line in lines)
                    {
                        string[] p = line.Split(new char[] { '\t', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (p.Length >= 7)
                        {
                            try
                            {
                                var eo = new EoExtentRecord { Id = p[0], X = double.Parse(p[1]), Y = double.Parse(p[2]), Z = double.Parse(p[3]), Kappa = double.Parse(p[6]) };

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

                                Dispatcher.Invoke(() => {
                                    _eoList.Add(eo);
                                    idx++;
                                    PrgStatus.Value = (double)idx / lines.Length * 100;
                                    TxtStatus.Text = $"{idx} / {lines.Length} 스캔 중...";
                                });
                            }
                            catch { }
                        }
                    }

                    Dispatcher.Invoke(() => TxtStatus.Text = "영역 병합(Union) 계산 중...");
                    _baseMergedGeometry = CascadedPolygonUnion.Union(allPolygons);
                });

                ApplyBufferAndDraw();

                await ShowModernDialog("분석 완료", $"총 {_eoList.Count}개의 EO 영역이 하나의 외곽선으로 병합되었습니다.");
            }
            catch (Exception ex)
            {
                await ShowModernDialog("오류", $"파일 로드 중 오류 발생: {ex.Message}");
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

            // 면적을 m²에서 km²로 변경 산출 (1,000,000으로 나누기)
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

            if (!ReferenceEquals(_baseMergedGeometry, _bufferedGeometry))
            {
                DrawSingleGeometryToCanvas(_bufferedGeometry,
                                           Color.FromRgb(243, 156, 18),
                                           Color.FromArgb(80, 241, 196, 15),
                                           cx, cy, scale, canvasCx, canvasCy);
            }

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

        // ★ System.Windows.Point 명시
        private System.Windows.Point MapPoint(Coordinate c, double cx, double cy, double scale, double canvasCx, double canvasCy)
        {
            double x = (c.X - cx) * scale + canvasCx;
            double y = -(c.Y - cy) * scale + canvasCy;
            return new System.Windows.Point(x, y);
        }

        // ==========================================
        // ★ 마우스 이벤트 처리 영역 (줌 & 팬)
        // ==========================================
        private void PreviewBorder_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.2 : (1.0 / 1.2);

            // ★ System.Windows.Point 명시
            System.Windows.Point mousePos = e.GetPosition(PreviewCanvas);

            CanvasScale.ScaleX *= zoomFactor;
            CanvasScale.ScaleY *= zoomFactor;

            if (CanvasScale.ScaleX < 0.1) CanvasScale.ScaleX = 0.1;
            if (CanvasScale.ScaleX > 50) CanvasScale.ScaleX = 50;
            if (CanvasScale.ScaleY < 0.1) CanvasScale.ScaleY = 0.1;
            if (CanvasScale.ScaleY > 50) CanvasScale.ScaleY = 50;

            CanvasTranslate.X -= mousePos.X * (CanvasScale.ScaleX - CanvasScale.ScaleX / zoomFactor);
            CanvasTranslate.Y -= mousePos.Y * (CanvasScale.ScaleY - CanvasScale.ScaleY / zoomFactor);
        }

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
                // ★ System.Windows.Point 및 System.Windows.Vector 명시
                System.Windows.Point currentPos = e.GetPosition(PreviewBorder);
                System.Windows.Vector delta = currentPos - _startPanPosition;

                CanvasTranslate.X += delta.X;
                CanvasTranslate.Y += delta.Y;

                _startPanPosition = currentPos;
            }
        }

        private void PreviewBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                PreviewBorder.ReleaseMouseCapture();
            }
        }

        private void PreviewBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                PreviewBorder.ReleaseMouseCapture();
            }
        }

        // ==========================================
        // SHP 파일 출력 영역
        // ==========================================
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