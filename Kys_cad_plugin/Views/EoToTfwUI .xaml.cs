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

        // EO 로드 및 비동기 계산 (ProgressBar 적용)
        private async void BtnLoadEo_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "텍스트 파일 (*.txt;*.csv)|*.txt;*.csv" };
            if (openFileDialog.ShowDialog() != true) return;

            try
            {
                if (!double.TryParse(TxtFocal.Text, out double focal) || !double.TryParse(TxtPixelSize.Text, out double pxSize) ||
                    !int.TryParse(TxtWidth.Text, out int wPx) || !int.TryParse(TxtHeight.Text, out int hPx))
                {
                    await ShowModernDialog("오류", "센서 제원을 확인해주세요.");
                    return;
                }

                _eoList.Clear(); _calcList.Clear();
                PrgStatus.Value = 0; TxtProgress.Text = "0%";

                double pxSizeM = pxSize / 1000000.0;
                bool ignoreK = ChkIgnoreKappa.IsChecked == true;

                // 1. 파일 전체 라인 먼저 읽기
                string[] allLines = File.ReadAllLines(openFileDialog.FileName, Encoding.Default);
                int totalRows = allLines.Length;

                // 2. 비동기 처리 시작
                await Task.Run(() =>
                {
                    int processed = 0;
                    foreach (string line in allLines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) { processed++; continue; }
                        string[] p = line.Split(new char[] { '\t', ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                        if (p.Length >= 7)
                        {
                            try
                            {
                                var eo = new EoRecord
                                {
                                    Id = p[0],
                                    X = double.Parse(p[1]),
                                    Y = double.Parse(p[2]),
                                    Z = double.Parse(p[3]),
                                    Omega = double.Parse(p[4]),
                                    Phi = double.Parse(p[5]),
                                    Kappa = double.Parse(p[6])
                                };

                                // 옵션 체크 시 Kappa를 0으로 강제 처리
                                double targetKappa = ignoreK ? 0 : eo.Kappa;
                                double kRad = targetKappa * (Math.PI / 180.0);
                                double gsd = (eo.Z / focal) * pxSizeM;

                                // 회전 행렬 적용 좌상단 계산
                                double halfW = (wPx - 1) * gsd / 2.0;
                                double halfH = (hPx - 1) * gsd / 2.0;
                                double offX = (-halfW * Math.Cos(kRad)) - (halfH * Math.Sin(kRad));
                                double offY = (-halfW * Math.Sin(kRad)) + (halfH * Math.Cos(kRad));

                                // UI 스레드에 데이터 추가 및 프로그레스 업데이트
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
                                    if (processed % 50 == 0 || processed == totalRows)
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
                        else { processed++; }
                    }
                });

                await ShowModernDialog("완료", $"{_calcList.Count}건의 데이터 계산이 완료되었습니다.");
            }
            catch (Exception ex) { await ShowModernDialog("오류", ex.Message); }
        }

        // TFW 저장
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