using Autodesk.AutoCAD.DatabaseServices;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;
using Kys_cad_plugin.Core; // ★ 중앙 데이터 매니저 참조 추가

namespace Kys_cad_plugin.Views
{
    public class EoRecord
    {
        public string Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Omega { get; set; }
        public double Phi { get; set; }
        public double Kappa { get; set; }
    }

    public class CalculatedTfwRecord
    {
        public string Id { get; set; }
        public double Gsd { get; set; }
        public double TopLeftX { get; set; }
        public double TopLeftY { get; set; }
        public double AppliedKappa { get; set; } // 실제 TFW에 계산 반영된 각도
    }

    public partial class EoToTfwUI : FluentWindow
    {
        private ObservableCollection<EoRecord> _eoList = new ObservableCollection<EoRecord>();
        private ObservableCollection<CalculatedTfwRecord> _calcList = new ObservableCollection<CalculatedTfwRecord>();

        public EoToTfwUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

            EoDataGrid.ItemsSource = _eoList;
            CalcDataGrid.ItemsSource = _calcList;
            CboSensorType.SelectedIndex = 0;
        }

        private void CboSensorType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (TxtWidth == null) return;
            string selected = (CboSensorType.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content.ToString();

            switch (selected)
            {
                case "DMC1": SetSensorValues("7680", "13824", "0.12", "12", true); break;
                case "DMC2": SetSensorValues("14144", "15552", "0.092", "5.6", true); break;
                case "DMC3": SetSensorValues("14592", "25728", "0.092", "3.9", true); break;
                case "Osprey4.1": SetSensorValues("14016", "20544", "0.08", "3.76", true); break;
                case "CountryMapper": SetSensorValues("31520", "13440", "0.107", "3.76", true); break;
                case "CountryMapper(90)": SetSensorValues("13440", "31520", "0.107", "3.76", true); break;
                case "사용자입력": SetSensorValues("", "", "", "", false); break;
            }
        }

        private void SetSensorValues(string w, string h, string f, string p, bool isReadOnly)
        {
            TxtWidth.Text = w; TxtHeight.Text = h; TxtFocal.Text = f; TxtPixelSize.Text = p;
            TxtWidth.IsReadOnly = TxtHeight.IsReadOnly = TxtFocal.IsReadOnly = TxtPixelSize.IsReadOnly = isReadOnly;
        }

        // [수정된 로직] 중앙 임포트 매니저 연동 및 비동기 계산
        private async void BtnLoadEo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 센서 제원 유효성 먼저 확인
                if (!double.TryParse(TxtFocal.Text, out double focal) || !double.TryParse(TxtPixelSize.Text, out double pxSize) ||
                    !int.TryParse(TxtWidth.Text, out int wPx) || !int.TryParse(TxtHeight.Text, out int hPx))
                {
                    await ShowModernDialog("오류", "센서 제원을 먼저 확인해주세요.");
                    return;
                }

                // 1. 필요한 7개 필드 정의
                var targetFields = new List<string> { "ID", "X", "Y", "Z", "Omega", "Phi", "Kappa" };

                // 2. 중앙 매니저 호출 (파일 로드 및 매핑 창)
                var result = await DataImportManager.ImportAndMap(this, targetFields);

                if (result == null || result.Rows.Count == 0) return;

                // 3. UI 초기화
                _eoList.Clear();
                _calcList.Clear();
                PrgStatus.Value = 0;
                TxtProgress.Text = "0%";

                double pxSizeM = pxSize / 1000000.0;
                bool ignoreK = ChkIgnoreKappa.IsChecked == true;
                int totalRows = result.Rows.Count;

                // 4. 수집된 데이터를 바탕으로 GSD 및 좌상단 좌표 계산 (비동기 처리)
                await Task.Run(() =>
                {
                    int processed = 0;
                    foreach (var row in result.Rows)
                    {
                        try
                        {
                            var eo = new EoRecord
                            {
                                Id = row["ID"],
                                X = double.Parse(row["X"]),
                                Y = double.Parse(row["Y"]),
                                Z = double.Parse(row["Z"]),
                                Omega = double.Parse(row["Omega"]),
                                Phi = double.Parse(row["Phi"]),
                                Kappa = double.Parse(row["Kappa"])
                            };

                            // 계산 로직 실행
                            double targetKappa = ignoreK ? 0 : eo.Kappa;
                            double kRad = targetKappa * (Math.PI / 180.0);
                            double gsd = (eo.Z / focal) * pxSizeM;

                            // 회전 행렬 적용 좌상단 계산
                            double halfW = (wPx - 1) * gsd / 2.0;
                            double halfH = (hPx - 1) * gsd / 2.0;
                            double offX = (-halfW * Math.Cos(kRad)) - (halfH * Math.Sin(kRad));
                            double offY = (-halfW * Math.Sin(kRad)) + (halfH * Math.Cos(kRad));

                            // UI 스레드에 결과 추가 및 프로그레스 업데이트
                            Dispatcher.Invoke(() => {
                                _eoList.Add(eo);
                                _calcList.Add(new CalculatedTfwRecord
                                {
                                    Id = eo.Id,
                                    Gsd = gsd,
                                    AppliedKappa = targetKappa,
                                    TopLeftX = eo.X + offX,
                                    TopLeftY = eo.Y + offY
                                });

                                processed++;
                                if (processed % 20 == 0 || processed == totalRows)
                                {
                                    double pct = (double)processed / totalRows * 100;
                                    PrgStatus.Value = pct;
                                    TxtProgress.Text = $"{(int)pct}%";
                                    TxtCountStatus.Text = $"{processed} / {totalRows}";
                                }
                            });
                        }
                        catch { processed++; }
                    }
                });

                await ShowModernDialog("완료", $"{_calcList.Count}건의 데이터 변환 및 계산이 완료되었습니다.");
            }
            catch (Exception ex) { await ShowModernDialog("오류", ex.Message); }
        }

        // TFW 저장 (기존 로직 유지)
        private async void BtnSaveTfw_Click(object sender, RoutedEventArgs e)
        {
            if (_calcList.Count == 0) return;

            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FolderName;
                int count = 0;

                await Task.Run(() =>
                {
                    foreach (var data in _calcList)
                    {
                        double kRad = data.AppliedKappa * (Math.PI / 180.0);
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine((Math.Cos(kRad) * data.Gsd).ToString("F10"));
                        sb.AppendLine((Math.Sin(kRad) * data.Gsd).ToString("F10"));
                        sb.AppendLine((Math.Sin(kRad) * data.Gsd).ToString("F10"));
                        sb.AppendLine((-Math.Cos(kRad) * data.Gsd).ToString("F10"));
                        sb.AppendLine(data.TopLeftX.ToString("F10"));
                        sb.AppendLine(data.TopLeftY.ToString("F10"));

                        File.WriteAllText(Path.Combine(path, $"{data.Id}.tfw"), sb.ToString());
                        count++;
                    }
                });
                await ShowModernDialog("성공", $"{count}개의 TFW 파일이 저장되었습니다.");
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