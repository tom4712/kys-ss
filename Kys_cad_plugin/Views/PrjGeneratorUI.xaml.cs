using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using Wpf.Ui.Controls;

namespace Kys_cad_plugin.Views
{
    // ★ 1. 속성(Property)은 반드시 public string { get; set; } 형태여야 리스트에 나타납니다.
    public class SelectedFileInfo
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public string FolderPath { get; set; }
    }

    public partial class PrjGeneratorUI : FluentWindow
    {
        // 데이터가 추가되면 자동으로 UI에 알려주는 특수 리스트
        private ObservableCollection<SelectedFileInfo> _selectedFiles = new ObservableCollection<SelectedFileInfo>();

        public PrjGeneratorUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

            // ★ 2. 이 코드가 없으면 파일 추가 버튼을 눌러도 리스트에 안 뜹니다!
            // 리스트뷰와 실제 데이터 뭉치를 연결합니다.
            FileListView.ItemsSource = _selectedFiles;

            // 좌표계 리스트 초기화
            CboPrjType.ItemsSource = _prjDict.Keys;
            CboPrjType.SelectedIndex = 2; // '중부' 기본값
        }

        // 13개 좌표계 딕셔너리 (기존과 동일)
        private readonly Dictionary<string, string> _prjDict = new Dictionary<string, string>
        {
            { "서해", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"scale_factor\",1],PARAMETER[\"central_meridian\",123],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",600000],UNIT[\"Meter\",1]]" },
            { "서부", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",125],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",600000],UNIT[\"Meter\",1]]" },
            { "중부", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",127],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",600000],UNIT[\"Meter\",1]]" },
            { "동부", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",129],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",600000],UNIT[\"Meter\",1]]" },
            { "동해", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",131],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",600000],UNIT[\"Meter\",1]]" },
            { "UTMK", "PROJCS[\"PCS_ITRF2000_TM\",GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS84\",SPHEROID[\"WGS84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"Degree\",0.017453292519943295]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",127.5],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",1000000],PARAMETER[\"false_northing\",2000000],UNIT[\"Meter\",1]]" },
            { "UTM52N", "PROJCS[\"UTM_Zone_52_Northern_Hemisphere\",GEOGCS[\"GCS_Geographic Coordinate System\",DATUM[\"D_WGS84\",SPHEROID[\"WGS84\",6378137,298.257223560493]],PRIMEM[\"Greenwich\",0],UNIT[\"Degree\",0.017453292519943295]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",129],PARAMETER[\"scale_factor\",0.9996],PARAMETER[\"false_easting\",500000],PARAMETER[\"false_northing\",0],UNIT[\"Meter\",1]]" },
            { "경위도", "GEOGCS[\"Geographic Coordinate System\",DATUM[\"D_WGS84\",SPHEROID[\"WGS84\",6378137,298.257223560493]],PRIMEM[\"Greenwich\",0],UNIT[\"Degree\",0.017453292519943295]]" },
            { "서해(50만)", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",123],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",500000],UNIT[\"Meter\",1]]" },
            { "서부(50만)", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",125],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",500000],UNIT[\"Meter\",1]]" },
            { "중부(50만)", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",127],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",500000],UNIT[\"Meter\",1]]" },
            { "동부(50만)", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",129],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",500000],UNIT[\"Meter\",1]]" },
            { "동해(50만)", "PROJCS[\"Transverse_Mercator\",GEOGCS[\"Geographic Coordinate System\",DATUM[\"WGS84\",SPHEROID[\"GRS 1980\",6378137,298.2572220960423]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"],PARAMETER[\"central_meridian\",131],PARAMETER[\"latitude_of_origin\",38],PARAMETER[\"false_easting\",200000],PARAMETER[\"false_northing\",500000],UNIT[\"Meter\",1]]" }
        };

        private void BtnLoadFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Multiselect = true };
            if (dialog.ShowDialog() == true)
            {
                // 기존 리스트 유지하면서 추가할지, 새로 덮어씌울지 결정 (지금은 덮어씌우기)
                _selectedFiles.Clear();
                foreach (string path in dialog.FileNames)
                {
                    _selectedFiles.Add(new SelectedFileInfo
                    {
                        FileName = Path.GetFileName(path),
                        FullPath = path,
                        FolderPath = Path.GetDirectoryName(path)
                    });
                }
                TxtStatus.Text = $"{_selectedFiles.Count} 개";
            }
        }

        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFiles.Count == 0) return;
            string prjWkt = _prjDict[CboPrjType.SelectedItem.ToString()];

            try
            {
                foreach (var file in _selectedFiles)
                {
                    string prjPath = Path.Combine(file.FolderPath, Path.GetFileNameWithoutExtension(file.FullPath) + ".prj");
                    File.WriteAllText(prjPath, prjWkt, Encoding.UTF8);
                }

                var msg = new Wpf.Ui.Controls.MessageBox
                {
                    Title = "완료",
                    Content = new System.Windows.Controls.TextBlock { Text = "PRJ 파일 생성이 완료되었습니다." },
                    CloseButtonText = "확인"
                };
                await msg.ShowDialogAsync();
            }
            catch (Exception ex) { /* 에러 처리 */ }
        }
    }
}