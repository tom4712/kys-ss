using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls; // Fluent UI

namespace Kys_cad_plugin.Views
{
    public partial class EoIdChangeUI : FluentWindow
    {
        private List<EoData> _allEoData = new List<EoData>();
        public ObservableCollection<CourseGroup> CourseGroups { get; set; } = new ObservableCollection<CourseGroup>();

        public EoIdChangeUI()
        {
            InitializeComponent();
            CourseDataGrid.ItemsSource = CourseGroups;
        }

        // ★ Fluent UI 전용 메시지 박스를 띄우기 위한 비동기 헬퍼 메서드
        private async Task ShowUiMessage(string title, string content)
        {
            var uiMessageBox = new Wpf.Ui.Controls.MessageBox
            {
                Title = title,
                Content = content,
                CloseButtonText = "확인" // Wpf.Ui 버전에 따라 PrimaryButtonText = "확인" 으로 변경해야 할 수도 있습니다.
            };

            await uiMessageBox.ShowDialogAsync();
        }

        // 1. 파일 찾기 버튼
        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "텍스트 파일 (*.txt)|*.txt|모든 파일 (*.*)|*.*",
                Title = "원본 EO 데이터 선택"
            };

            if (ofd.ShowDialog() == true)
            {
                TxtFilePath.Text = ofd.FileName;
            }
        }

        // 2. 코스 분석 (비동기 async 적용)
        private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtFilePath.Text) || !File.Exists(TxtFilePath.Text))
            {
                await ShowUiMessage("알림", "먼저 유효한 EO 파일을 선택해주세요.");
                return;
            }

            if (!double.TryParse(TxtTimeGap.Text, out double timeGapThreshold))
            {
                await ShowUiMessage("오류", "시간차(초)는 숫자로 입력해주세요.");
                return;
            }

            try
            {
                _allEoData.Clear();
                CourseGroups.Clear();

                string[] lines = File.ReadAllLines(TxtFilePath.Text);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] parts = line.Split(new char[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length >= 8)
                    {
                        if (double.TryParse(parts[1], out double gpsTime))
                        {
                            _allEoData.Add(new EoData
                            {
                                OriginalId = parts[0],
                                GpsTime = gpsTime,
                                X = parts[2],
                                Y = parts[3],
                                Z = parts[4],
                                Omg = parts[5],
                                Phi = parts[6],
                                Kap = parts[7]
                            });
                        }
                    }
                }

                if (_allEoData.Count == 0)
                {
                    await ShowUiMessage("알림", "데이터를 인식하지 못했습니다. 파일 형식을 확인하세요.");
                    return;
                }

                int courseIndex = 1;
                CourseGroup currentGroup = new CourseGroup
                {
                    CourseIndex = courseIndex,
                    OldStartId = _allEoData[0].OriginalId,
                    NewCourseName = $"C{courseIndex:D2}_"
                };
                currentGroup.EoItems.Add(_allEoData[0]);

                for (int i = 1; i < _allEoData.Count; i++)
                {
                    double timeDiff = Math.Abs(_allEoData[i].GpsTime - _allEoData[i - 1].GpsTime);

                    if (timeDiff > timeGapThreshold)
                    {
                        CourseGroups.Add(currentGroup);
                        courseIndex++;
                        currentGroup = new CourseGroup
                        {
                            CourseIndex = courseIndex,
                            OldStartId = _allEoData[i].OriginalId,
                            NewCourseName = $"C{courseIndex:D2}_"
                        };
                    }
                    currentGroup.EoItems.Add(_allEoData[i]);
                }
                CourseGroups.Add(currentGroup);

            }
            catch (Exception ex)
            {
                await ShowUiMessage("오류", $"파일 분석 중 오류가 발생했습니다: {ex.Message}");
            }
        }

        // 3. 변환된 데이터 내보내기 (비동기 async 적용)
        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (CourseGroups.Count == 0)
            {
                await ShowUiMessage("알림", "먼저 데이터를 불러오고 분석을 실행해주세요.");
                return;
            }

            bool isCsv = RbExcel.IsChecked == true;
            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = isCsv ? "Excel CSV 파일 (*.csv)|*.csv" : "텍스트 파일 (*.txt)|*.txt",
                FileName = "Updated_EO_Data",
                Title = "새로운 EO 데이터 저장"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(sfd.FileName))
                    {
                        string sep = isCsv ? "," : " ";

                        if (isCsv) sw.WriteLine("ID,GPSTime,X,Y,Z,Omega,Phi,Kappa");

                        foreach (var group in CourseGroups)
                        {
                            string prefix = group.NewCourseName ?? "";
                            int startNum = group.NewStartNumber;

                            for (int i = 0; i < group.EoItems.Count; i++)
                            {
                                var eo = group.EoItems[i];
                                string newId = $"{prefix}{(startNum + i)}";

                                sw.WriteLine($"{newId}{sep}{eo.GpsTime:F6}{sep}{eo.X}{sep}{eo.Y}{sep}{eo.Z}{sep}{eo.Omg}{sep}{eo.Phi}{sep}{eo.Kap}");
                            }
                        }
                    }
                    await ShowUiMessage("완료", "성공적으로 저장되었습니다!");
                }
                catch (Exception ex)
                {
                    await ShowUiMessage("오류", $"저장 중 오류가 발생했습니다: {ex.Message}");
                }
            }
        }
    }

    // --- 데이터를 담을 모델 클래스 ---
    public class EoData
    {
        public string OriginalId { get; set; }
        public double GpsTime { get; set; }
        public string X { get; set; }
        public string Y { get; set; }
        public string Z { get; set; }
        public string Omg { get; set; }
        public string Phi { get; set; }
        public string Kap { get; set; }
    }

    public class CourseGroup
    {
        public int CourseIndex { get; set; }
        public List<EoData> EoItems { get; set; } = new List<EoData>();

        public int PhotoCount => EoItems.Count;
        public string OldStartId { get; set; }

        public string NewCourseName { get; set; }
        public int NewStartNumber { get; set; } = 1;
    }
}