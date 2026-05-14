using Kys_cad_plugin.Core; // ★ 중앙 데이터 매니저 참조
using Microsoft.Win32;
using NetTopologySuite.Features;
// GIS 라이브러리
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.Collections.ObjectModel;
using System.Windows;
using Wpf.Ui.Controls;

namespace Kys_cad_plugin.Views
{
    public class EoAreaRecord
    {
        public string Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Omega { get; set; } // ★ 추가
        public double Phi { get; set; }   // ★ 추가
        public double Kappa { get; set; }
    }

    public class FootprintRecord
    {
        public string Id { get; set; }
        public string TL { get; set; } // 좌상단
        public string TR { get; set; } // 우상단
        public string BL { get; set; } // 좌하단
        public string BR { get; set; } // 우하단
        public Coordinate[] Coords { get; set; } // SHP용 원본 좌표
    }

    public partial class EoToAreaShpUI : FluentWindow
    {
        private ObservableCollection<EoAreaRecord> _eoList = new ObservableCollection<EoAreaRecord>();
        private ObservableCollection<FootprintRecord> _footprintList = new ObservableCollection<FootprintRecord>();

        public EoToAreaShpUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
            EoDataGrid.ItemsSource = _eoList;
            AreaDataGrid.ItemsSource = _footprintList;
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

        // [수정] 7개 모든 컬럼 매핑 연동
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

                // ★ 1. 7개 모든 컬럼 필드 정의
                var targetFields = new List<string> { "ID", "X", "Y", "Z", "Omega", "Phi", "Kappa" };

                // 2. 중앙 매니저 호출 (파일 로드 및 매핑 팝업)
                var result = await DataImportManager.ImportAndMap(this, targetFields);

                if (result == null || result.Rows.Count == 0) return;

                // 3. UI 초기화 및 설정
                double pxSizeM = pxSizeMicron / 1000000.0;
                _eoList.Clear();
                _footprintList.Clear();
                PrgStatus.Value = 0;
                TxtStatus.Text = "데이터 분석 중...";

                int totalRows = result.Rows.Count;

                // 4. 영역 계산 비동기 처리
                await Task.Run(() =>
                {
                    int idx = 0;
                    foreach (var row in result.Rows)
                    {
                        try
                        {
                            var eo = new EoAreaRecord
                            {
                                Id = row["ID"],
                                X = double.Parse(row["X"]),
                                Y = double.Parse(row["Y"]),
                                Z = double.Parse(row["Z"]),
                                Omega = double.Parse(row["Omega"]), // 파싱 추가
                                Phi = double.Parse(row["Phi"]),     // 파싱 추가
                                Kappa = double.Parse(row["Kappa"])
                            };

                            // GSD 및 영역 계산 로직
                            double gsd = (eo.Z / focal) * pxSizeM;
                            double kRad = eo.Kappa * (Math.PI / 180.0);
                            double hW = (wPx * gsd) / 2.0;
                            double hH = (hPx * gsd) / 2.0;

                            Coordinate tl = Rotate(eo.X, eo.Y, -hW, hH, kRad);
                            Coordinate tr = Rotate(eo.X, eo.Y, hW, hH, kRad);
                            Coordinate br = Rotate(eo.X, eo.Y, hW, -hH, kRad);
                            Coordinate bl = Rotate(eo.X, eo.Y, -hW, -hH, kRad);

                            Dispatcher.Invoke(() =>
                            {
                                _eoList.Add(eo);
                                _footprintList.Add(new FootprintRecord
                                {
                                    Id = eo.Id,
                                    TL = $"{tl.X:F2}, {tl.Y:F2}",
                                    TR = $"{tr.X:F2}, {tr.Y:F2}",
                                    BL = $"{bl.X:F2}, {bl.Y:F2}",
                                    BR = $"{br.X:F2}, {br.Y:F2}",
                                    Coords = new[] { tl, tr, br, bl, tl }
                                });
                                idx++;

                                if (idx % 20 == 0 || idx == totalRows)
                                {
                                    PrgStatus.Value = (double)idx / totalRows * 100;
                                    TxtStatus.Text = $"{idx} / {totalRows} 처리 중...";
                                }
                            });
                        }
                        catch { idx++; }
                    }
                });

                await ShowModernDialog("분석 완료", $"{_footprintList.Count}건의 영역 계산이 완료되었습니다.");
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

        private async void BtnExportShp_Click(object sender, RoutedEventArgs e)
        {
            if (_footprintList.Count == 0)
            {
                await ShowModernDialog("알림", "저장할 데이터가 없습니다.");
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog { Filter = "Shapefile (*.shp)|*.shp", FileName = $"Footprints_{DateTime.Now:yyyyMMdd}" };
            if (sfd.ShowDialog() != true) return;

            try
            {
                var factory = new GeometryFactory();
                var features = new List<Feature>();

                foreach (var foot in _footprintList)
                {
                    var poly = factory.CreatePolygon(foot.Coords);
                    var attr = new AttributesTable();
                    attr.Add("ID", foot.Id);
                    attr.Add("TL", foot.TL);
                    attr.Add("TR", foot.TR);
                    attr.Add("BL", foot.BL);
                    attr.Add("BR", foot.BR);
                    features.Add(new Feature(poly, attr));
                }

                var writer = new ShapefileDataWriter(sfd.FileName) { Header = ShapefileDataWriter.GetHeader(features[0], features.Count) };
                writer.Write(features);

                await ShowModernDialog("성공", $"SHP 파일이 생성되었습니다.\n경로: {sfd.FileName}");
            }
            catch (Exception ex)
            {
                await ShowModernDialog("저장 오류", ex.Message);
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