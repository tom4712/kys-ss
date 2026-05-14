using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace Kys_cad_plugin.Views
{
    public partial class ColumnMappingDialog : FluentWindow
    {
        public Dictionary<string, int> MappedIndices { get; private set; } = new Dictionary<string, int>();
        public int StartRow { get; private set; } = 1;

        private List<System.Windows.Controls.ComboBox> _comboBoxes = new List<System.Windows.Controls.ComboBox>();

        public ColumnMappingDialog(string[] sampleData, List<string> requiredFields)
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

            // 1. 미리보기 텍스트 설정
            TxtPreview.Text = string.Join("  |  ", sampleData);

            // 2. 자동 시작 행 판단 로직
            bool isHeader = false;
            if (sampleData.Length > 1)
            {
                for (int i = 1; i < sampleData.Length; i++)
                {
                    // 숫자로 변환 불가능한 값이 하나라도 있으면 헤더로 간주
                    if (!double.TryParse(sampleData[i], out _))
                    {
                        isHeader = true;
                        break;
                    }
                }
            }
            NumStartRow.Value = isHeader ? 2 : 1;

            // 3. 콤보박스 아이템 리스트 생성
            List<string> columns = new List<string>();
            for (int i = 0; i < sampleData.Length; i++)
            {
                columns.Add($"[{i + 1}열] {sampleData[i]}");
            }

            // 4. 동적 필드 UI 생성
            foreach (var field in requiredFields)
            {
                var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // ★ 'TextBlock' 대신 전체 경로 명시하여 모호성 해결
                var label = new System.Windows.Controls.TextBlock
                {
                    Text = field,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold
                };
                Grid.SetColumn(label, 0);

                var combo = new System.Windows.Controls.ComboBox
                {
                    ItemsSource = columns,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Tag = field
                };
                Grid.SetColumn(combo, 1);

                int defaultIdx = requiredFields.IndexOf(field);
                if (defaultIdx < columns.Count) combo.SelectedIndex = defaultIdx;

                rowGrid.Children.Add(label);
                rowGrid.Children.Add(combo);
                DynamicFieldsPanel.Children.Add(rowGrid);
                _comboBoxes.Add(combo);
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            MappedIndices.Clear();
            foreach (var cb in _comboBoxes)
            {
                MappedIndices[cb.Tag.ToString()] = cb.SelectedIndex;
            }

            StartRow = (int)(NumStartRow.Value ?? 1);
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}