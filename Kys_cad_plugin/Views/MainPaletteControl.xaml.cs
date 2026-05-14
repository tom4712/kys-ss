using Kys_cad_plugin.Core;
using MahApps.Metro.IconPacks; // 6.2.1.0 버전용
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Kys_cad_plugin.Views
{



    public partial class MainPaletteControl : UserControl
    {
        private bool _isLicensed = false;
        // [핵심 1] 단 하나의 "현재 활성화된 창"만 기억합니다.
        private static Window _currentActiveWindow = null;


        private const string VersionCsvUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vQ2mnccfwMdM7PqsV4Om_-kyCBNl-EqIwzGczYkV7fmWZmtRl0nqfR-FGz5PwkWpZliuHqVezNlFpTV/pub?gid=0&single=true&output=csv";

        private async void VersionUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Version currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                string currentVerStr = $"{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build}";

                string latestVerStr = await GetLatestVersionFromSheet();

                if (latestVerStr == "ERROR")
                {
                    await ShowSimpleDialog("연결 오류", "서버 연결에 실패했습니다.");
                    return;
                }

                Version latestVersion = new Version(latestVerStr);
                bool isUpdateNeeded = latestVersion > currentVersion;

                // 1. 다이얼로그 생성 (공통 설정만)
                var dialog = new Wpf.Ui.Controls.ContentDialog(DialogHost)
                {
                    Title = "시스템 버전 정보",
                    Content = null, // 아래에서 생성한 stack을 대입할 예정
                    PrimaryButtonText = isUpdateNeeded ? "업데이트 문의" : "확인",
                    // ★ 파란색 배경 강제 적용 (가시성 확보)
                    PrimaryButtonAppearance = ControlAppearance.Info,
                    DefaultButton = ContentDialogButton.Primary
                };

                // 2. ★ 핵심: 업데이트가 필요할 때만 CloseButtonText를 할당
                // 필요 없을 때는 아예 대입조차 안 해야 저 유령 버튼이 안 생깁니다.
                if (isUpdateNeeded)
                {
                    dialog.CloseButtonText = "나중에";
                }

                // 3. 내부 콘텐츠 (UI 가독성 향상)
                var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
                stack.Children.Add(new Wpf.Ui.Controls.TextBlock
                {
                    Text = isUpdateNeeded ? "⚠ 새로운 버전 업데이트가 필요합니다." : "✅ 최신 버전을 사용 중입니다.",
                    FontWeight = FontWeights.Bold,
                    FontSize = 15,
                    Foreground = isUpdateNeeded ? new SolidColorBrush(Color.FromRgb(255, 180, 0)) : new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                    Margin = new Thickness(0, 0, 0, 18)
                });

                stack.Children.Add(new Wpf.Ui.Controls.TextBlock { Text = $"현재 버전: v{currentVerStr}", Margin = new Thickness(0, 0, 0, 5), FontSize = 13 });
                stack.Children.Add(new Wpf.Ui.Controls.TextBlock { Text = $"최신 버전: v{latestVerStr}", FontWeight = FontWeights.Bold, FontSize = 13 });

                dialog.Content = stack;

                // 4. 실행
                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary && isUpdateNeeded)
                {
                    await ShowAdminContactDialog();
                }
            }
            catch (Exception ex)
            {
                await ShowSimpleDialog("오류", ex.Message);
            }
        }

        // --- 헬퍼 함수들 (ControlAppearance.Primary 적용) ---

        private async Task ShowAdminContactDialog()
        {
            var adminDialog = new Wpf.Ui.Controls.ContentDialog(DialogHost)
            {
                Title = "업데이트 문의",
                PrimaryButtonText = "확인",
                PrimaryButtonAppearance = ControlAppearance.Info, // 버튼 강조
                Content = new Wpf.Ui.Controls.TextBlock
                {
                    Text = "최신 버전 설치 및 업데이트는\n관리자에게 문의하시기 바랍니다.\n\n[메일 : tom4712@kakao.com]\n[카톡 : zszszszszs]",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 0)
                }
            };
            await adminDialog.ShowAsync();
        }

        private async Task ShowSimpleDialog(string title, string content)
        {
            var simpleDialog = new Wpf.Ui.Controls.ContentDialog(DialogHost)
            {
                Title = title,
                PrimaryButtonText = "확인",
                PrimaryButtonAppearance = ControlAppearance.Primary, // 버튼 강조
                Content = new Wpf.Ui.Controls.TextBlock { Text = content, TextWrapping = TextWrapping.Wrap }
            };
            await simpleDialog.ShowAsync();
        }

        private async Task<string> GetLatestVersionFromSheet()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    // 1. 캐시 방지를 위해 현재 시간을 나노초 단위로 주소 뒤에 붙임
                    // 결과 예시: ...output=csv&t=63851234567890
                    string cacheBuster = DateTime.Now.Ticks.ToString();
                    string finalUrl = $"{VersionCsvUrl}&t={cacheBuster}";

                    client.Timeout = TimeSpan.FromSeconds(3);

                    // 2. 헤더에 캐시 쓰지 말라고 명시적으로 선언 (2중 안전장치)
                    client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
                    {
                        NoCache = true
                    };

                    string csvContent = await client.GetStringAsync(finalUrl);

                    if (!string.IsNullOrWhiteSpace(csvContent))
                    {
                        // 첫 줄 첫 칸 데이터 추출
                        string version = csvContent.Split('\n')[0].Split(',')[0].Trim();
                        return version;
                    }
                }
            }
            catch (Exception ex)
            {
                // 로그나 디버그 창에서 에러 확인용 (필요시)
                System.Diagnostics.Debug.WriteLine($"Version Fetch Error: {ex.Message}");
            }
            return "ERROR";
        }










        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Loaded 안에서도 테마 한 번 더 쐐기 박기
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);

            // 여기서 라이선스를 검사해서 UI 상태를 바꿉니다.
            CheckLicenseState();

            // 라이선스 유무와 상관없이 트리뷰 데이터는 뒤에서 미리 만들어 둡니다.
            LoadMenuFromTextFile();
        }

        private async void LicenseButton_Click(object sender, RoutedEventArgs e)
        {
            string savedKey = Kys_cad_plugin.Core.RegistryHelper.GetLicenseKey();

            if (string.IsNullOrEmpty(savedKey))
            {
                // 키가 없으면 바로 입력창
                await ShowInputLicenseDialogAsync();
            }
            else
            {
                // 키가 있으면 (만료되었더라도) 정보를 서버에서 가져와 상세창 표시
                var info = await Kys_cad_plugin.Core.LicenseManager.GetFullLicenseInfoAsync(savedKey);

                if (info != null)
                {
                    // ⭐️ [수정된 부분] 상세창을 띄우기 전에, 현재 기기 대수가 한도를 초과했는지 먼저 검사합니다.
                    if (info.CurrentMachines > info.MaxMachines)
                    {
                        // 초과 상태라면 경고창을 띄워 사용자에게 상황을 알립니다.
                        System.Windows.MessageBox.Show(
                            $"[인증 초과 알림]\n허용된 기기 대수({info.MaxMachines}대)를 초과하여 사용 중입니다.\n상세창 하단의 '기기에서 삭제'를 눌러 대수를 맞춰주세요.",
                            "기기 제한 초과",
                            System.Windows.MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }

                    // 상세창 표시 (초과 상태더라도 상세창을 띄워야 하단 링크로 '삭제'를 할 수 있음)
                    await ShowLicenseDetailsDialogAsync(info);
                }
                else
                {
                    // 서버 연결 실패 시에도 일단 입력창이라도 띄워줌
                    await ShowInputLicenseDialogAsync();
                }
            }
        }

        private async System.Threading.Tasks.Task ShowLicenseDetailsDialogAsync(Kys_cad_plugin.Core.LicenseInfo info)
        {
            var contentPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };

            // 일반 텍스트 행을 만드는 헬퍼
            void AddInfoRow(string label, string value)
            {
                var labelBlock = new Wpf.Ui.Controls.TextBlock { Text = label, FontSize = 11, Opacity = 0.6, Margin = new Thickness(0, 10, 0, 2) };
                var valueBlock = new Wpf.Ui.Controls.TextBlock { Text = value, FontSize = 13, Margin = new Thickness(0, 0, 0, 5) };
                contentPanel.Children.Add(labelBlock);
                contentPanel.Children.Add(valueBlock);
            }

            // --- [핵심 수정] 상태 행 커스텀 제작 ---
            var statusLabel = new Wpf.Ui.Controls.TextBlock { Text = "라이선스 상태", FontSize = 11, Opacity = 0.6, Margin = new Thickness(0, 10, 0, 2) };
            var statusValueBlock = new Wpf.Ui.Controls.TextBlock { Margin = new Thickness(0, 0, 0, 5) };

            // 1. "정품 인증됨" (크고 진하게)
            var mainStatus = new System.Windows.Documents.Run(info.IsValid ? "정품 인증됨" : "인증 만료")
            {
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = info.IsValid ? Brushes.LimeGreen : Brushes.OrangeRed
            };

            // 2. 라이선스 이름 (작고 회색으로)
            var nameStatus = new System.Windows.Documents.Run($"  {info.Name}")
            {
                FontSize = 11,
                Foreground = (Brush)new BrushConverter().ConvertFrom("#888888") // 연한 회색
            };

            statusValueBlock.Inlines.Add(mainStatus);
            statusValueBlock.Inlines.Add(nameStatus);

            contentPanel.Children.Add(statusLabel);
            contentPanel.Children.Add(statusValueBlock);
            // ---------------------------------------

            // 나머지 정보 표시
            AddInfoRow("등록된 키", info.Key);
            AddInfoRow("설치 가능 기기", $"{info.CurrentMachines} / {info.MaxMachines} 대");

            string timeLeft = info.Expiry.HasValue && info.Expiry.Value > DateTime.Now ?
                $"{(int)(info.Expiry.Value - DateTime.Now).TotalDays}일 남음" : "만료됨";
            AddInfoRow("만료 일자", $"{info.Expiry?.ToString("yyyy-MM-dd HH:mm") ?? "N/A"} ({timeLeft})");

            // 구분선
            contentPanel.Children.Add(new System.Windows.Shapes.Rectangle { Height = 1, Margin = new Thickness(0, 15, 0, 10), Opacity = 0.1, Fill = Brushes.Gray });

            // 하이퍼링크 섹션 (변경/삭제)
            var linkPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var linkTextBlock = new Wpf.Ui.Controls.TextBlock { FontSize = 12 };
            var changeLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("라이선스 변경")) { TextDecorations = null, Foreground = (Brush)new BrushConverter().ConvertFrom("#4A90E2") };
            var deleteLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("기기에서 삭제")) { TextDecorations = null, Foreground = (Brush)new BrushConverter().ConvertFrom("#E74C3C") };
            var divider = new System.Windows.Documents.Run("   |   ") { Foreground = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128)) };

            linkTextBlock.Inlines.Add(changeLink); linkTextBlock.Inlines.Add(divider); linkTextBlock.Inlines.Add(deleteLink);
            linkPanel.Children.Add(linkTextBlock); contentPanel.Children.Add(linkPanel);

            var dialog = new Wpf.Ui.Controls.ContentDialog(DialogHost) { Title = "라이선스 상세 정보", Content = contentPanel, CloseButtonText = "닫기" };

            changeLink.Click += async (s, e) => { dialog.Hide(); await ShowInputLicenseDialogAsync(); };
            deleteLink.Click += async (s, e) =>
            {
                // 1. WPF 기본 MessageBox로 사용자 확인
                if (System.Windows.MessageBox.Show("서버에서 기기를 해제합니다. 계속하시겠습니까?", "기기 삭제", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes)
                {
                    // 2. WPF-UI 다이얼로그 닫기 (이 부분을 안 닫아주면 UI가 멈춘 것처럼 보일 수 있음)
                    dialog.Hide();

                    // 3. 서버에 삭제 요청 (위에서 가져온 info.MachineId 사용)
                    var result = await Core.LicenseManager.DeactivateMachineAsync(info.Key, info.MachineId);

                    if (result.Success)
                    {
                        // 4. 레지스트리 키 삭제 및 UI 잠금 상태로 갱신
                        Core.RegistryHelper.DeleteLicenseKey();
                        System.Windows.MessageBox.Show("기기가 성공적으로 해제되었습니다.", "성공", System.Windows.MessageBoxButton.OK, MessageBoxImage.Information);

                        // 기존에 만들어두신 UI 갱신 함수 호출 (다시 자물쇠 화면으로 돌아감)
                        await CheckLicenseState();
                    }
                    else
                    {
                        System.Windows.MessageBox.Show($"해제 실패: {result.Message}", "오류", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };

            await dialog.ShowAsync();
        }

        // 상태 확인 함수를 비동기(async)로 변경
        private async System.Threading.Tasks.Task CheckLicenseState()
        {
            string savedKey = Kys_cad_plugin.Core.RegistryHelper.GetLicenseKey();

            // 키가 없으면 즉시 잠금
            if (string.IsNullOrEmpty(savedKey))
            {
                SetUIByLicenseStatus(isValid: false);
                return;
            }

            // 서버에 유효성 확인 (만료 여부 체크)
            var result = await Kys_cad_plugin.Core.LicenseManager.ValidateLicenseAsync(savedKey);

            if (result.IsValid)
            {
                // 정품 & 기간 남음
                SetUIByLicenseStatus(isValid: true);
            }
            else
            {
                // 키는 있으나 기간 만료됨 -> 잠금화면 로드 (정보 확인은 가능)
                SetUIByLicenseStatus(isValid: false);
            }
        }

        private void SetUIByLicenseStatus(bool isValid)
        {
            if (isValid)
            {
                // 1. 라이선스 유효: 잠금 해제
                LockArea.Visibility = Visibility.Collapsed;    // 잠금 아이콘/메시지 숨김
                CommandTree.Visibility = Visibility.Visible;   // 트리뷰 표시
                SearchBox.Visibility = Visibility.Visible;     // 검색창 표시

                // 하단 버튼 텍스트 변경
                LicenseBtnText.Text = "License 정보 확인";
            }
            else
            {
                // 2. 라이선스 만료 또는 미인증: 잠금 유지
                LockArea.Visibility = Visibility.Visible;      // 잠금 아이콘/메시지 표시
                CommandTree.Visibility = Visibility.Collapsed; // 트리뷰 숨김
                SearchBox.Visibility = Visibility.Collapsed;   // 검색창 숨김

                // 하단 버튼 텍스트 변경
                LicenseBtnText.Text = "License 인증하기";
            }
        }

        public MainPaletteControl()
        {
            // 1. 무조건 DLL 먼저 로드! (여기서 튕기면 안 됨)
            ForceLoadDependencies();

            // 2. 무조건 UI 컴포넌트 초기화!
            InitializeComponent();

            // 3. 무조건 다크 모드 테마 켜기!
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);

            // 4. 화면이 사용자에게 "보인 직후"에 라이선스 검사 및 메뉴 로드 실행
            this.Loaded += UserControl_Loaded;

            // 레지스트리에서 불러온 값 UI에 반영
            ToggleMaster.IsChecked = CommandSettings.IsPluginEnabled;

            // 이벤트 연결
            ToggleMaster.Checked += (s, e) => CommandSettings.IsPluginEnabled = true;
            ToggleMaster.Unchecked += (s, e) => CommandSettings.IsPluginEnabled = false;
        }

        private void LoadMenuFromTextFile()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                string resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(r => r.EndsWith("MenuConfig.txt", StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrEmpty(resourceName))
                {
                    string allResources = string.Join("\n", assembly.GetManifestResourceNames());
                    System.Windows.MessageBox.Show($"MenuConfig.txt를 찾을 수 없습니다.\n현재 리소스 목록:\n{allResources}");
                    return;
                }

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return;

                    using (StreamReader reader = new StreamReader(stream))
                    {
                        CommandTree.Items.Clear();
                        Wpf.Ui.Controls.TreeViewItem currentCategory = null;

                        while (!reader.EndOfStream)
                        {
                            string line = reader.ReadLine()?.Trim();
                            if (string.IsNullOrEmpty(line)) continue;

                            if (line.StartsWith("@"))
                            {
                                currentCategory = new Wpf.Ui.Controls.TreeViewItem
                                {
                                    Header = line.Substring(1).Trim(),
                                    IsExpanded = true,
                                    FontWeight = FontWeights.Bold,
                                    Focusable = false,
                                    Margin = new Thickness(0, 15, 0, 0)
                                };
                                CommandTree.Items.Add(currentCategory);
                            }
                            else if (line.StartsWith("#") && currentCategory != null)
                            {
                                string[] parts = line.Substring(1).Split('\\');
                                if (parts.Length >= 4)
                                {
                                    var menuItem = CreateMenuItem(parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), parts[3].Trim());
                                    currentCategory.Items.Add(menuItem);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"메뉴 로드 중 오류: {ex.Message}");
            }
        }

        private Wpf.Ui.Controls.TreeViewItem CreateMenuItem(string text, string iconName, string uiName, string colorHex)
        {
            StackPanel stack = new StackPanel { Orientation = Orientation.Horizontal };
            try
            {
                var brush = (Brush)new BrushConverter().ConvertFrom(colorHex);

                var icon = new PackIconFontAwesome
                {
                    Kind = (PackIconFontAwesomeKind)Enum.Parse(typeof(PackIconFontAwesomeKind), iconName),
                    Foreground = brush,
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                stack.Children.Add(icon);
            }
            catch { }

            stack.Children.Add(new Wpf.Ui.Controls.TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });

            return new Wpf.Ui.Controls.TreeViewItem
            {
                Header = stack,
                Tag = uiName,
                Padding = new Thickness(5, 2, 5, 2)
            };
        }

        private void CommandTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is Wpf.Ui.Controls.TreeViewItem item && item.Tag is string uiName)
            {
                try
                {
                    item.IsSelected = false; // 선택 즉시 해제

                    // 1. 메뉴에서 클릭한 항목의 텍스트(이름)를 가져와 창의 Title로 사용합니다.
                    string windowTitle = ExtractMenuTitle(item.Header);

                    // 2. 동적 창 생성 함수 호출 (이름과 UI 클래스명 전달)
                    OpenPluginWindow(windowTitle, uiName);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"'{uiName}' 실행 중 오류가 발생했습니다.\n{ex.Message}", "오류", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void OpenPluginWindow(string title, string uiClassName)
        {
            // 1. 기존에 열려있던 창 강제 종료 (싱글톤 유지)
            if (_currentActiveWindow != null)
            {
                if (_currentActiveWindow.IsLoaded) _currentActiveWindow.Close();
                _currentActiveWindow = null;
            }

            // 2. 텍스트 파일에 적힌 이름(uiClassName)으로 실제 C# 클래스(Type)를 찾습니다.
            string fullClassName = $"Kys_cad_plugin.Views.{uiClassName}";
            Type targetType = Assembly.GetExecutingAssembly().GetType(fullClassName);

            Window windowToShow = null;

            // 3. 찾은 클래스가 Window(FluentWindow 포함) 형태라면 그대로 생성합니다.
            if (targetType != null && targetType.IsSubclassOf(typeof(Window)))
            {
                windowToShow = (Window)Activator.CreateInstance(targetType);
                // Title은 XAML에 설정되어 있지만, 트리뷰에서 가져온 이름으로 덮어쓸 수도 있습니다.
                // windowToShow.Title = title; 
            }
            else
            {
                // 아직 개발 안 된 메뉴를 클릭했을 때 튕기지 않도록 방어 코드
                windowToShow = new Kys_cad_plugin.Views.TestWindow();
                windowToShow.Title = $"[미구현] {title}";
            }

            // 4. 창 닫힘 이벤트 처리
            windowToShow.Closed += (s, ev) =>
            {
                if (_currentActiveWindow == windowToShow) _currentActiveWindow = null;
            };

            // 5. 화면에 표시! (껍데기에 넣는 과정 없이 바로 Show)
            _currentActiveWindow = windowToShow;

            // ⭐️ 핵심: 여기서 생성되는 모든 플러그인 창을 무조건 최상단에 고정합니다!
            windowToShow.Topmost = true;

            windowToShow.Show();
        }

        // ==============================================================================
        // ⭐️ 보조 함수: TreeViewItem의 Header(StackPanel)에서 한글 텍스트만 쏙 빼오는 함수
        // ==============================================================================
        private string ExtractMenuTitle(object header)
        {
            if (header is StackPanel sp)
            {
                foreach (var child in sp.Children)
                {
                    if (child is Wpf.Ui.Controls.TextBlock tb)
                        return tb.Text; // 예: "좌표 입력", "특정문자 선택" 등
                }
            }
            return "KYS CAD Plugin";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = SearchBox.Text.ToLower().Trim();
            foreach (Wpf.Ui.Controls.TreeViewItem cat in CommandTree.Items)
            {
                bool hasVisibleChild = false;
                foreach (Wpf.Ui.Controls.TreeViewItem item in cat.Items)
                {
                    string headerText = GetHeaderText(item);
                    bool match = string.IsNullOrEmpty(filter) || headerText.Contains(filter);
                    item.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
                    if (match) hasVisibleChild = true;
                }
                cat.Visibility = hasVisibleChild ? Visibility.Visible : Visibility.Collapsed;
                if (!string.IsNullOrEmpty(filter) && hasVisibleChild) cat.IsExpanded = true;
            }
        }

        private string GetHeaderText(Wpf.Ui.Controls.TreeViewItem item)
        {
            if (item.Header is StackPanel sp)
            {
                foreach (var child in sp.Children) if (child is Wpf.Ui.Controls.TextBlock tb) return tb.Text.ToLower();
            }
            return "";
        }

        private void ForceLoadDependencies()
        {
            try
            {
                string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

                string[] dlls = {
                    "Wpf.Ui.dll",
                    "MahApps.Metro.IconPacks.Core.dll",
                    "MahApps.Metro.IconPacks.FontAwesome.dll"
                };

                foreach (var dll in dlls)
                {
                    string fullPath = Path.Combine(dir, dll);

                    if (!File.Exists(fullPath))
                    {
                        System.Windows.MessageBox.Show($"[필수 파일 누락] {dll} 파일이 없습니다!\n경로: {fullPath}");
                    }
                    else
                    {
                        Assembly.LoadFrom(fullPath);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"DLL 로드 중 에러: {ex.Message}");
            }
        }




        private async System.Threading.Tasks.Task ShowInputLicenseDialogAsync()
        {
            var textBox = new Wpf.Ui.Controls.TextBox
            {
                PlaceholderText = "XXXX-XXXX-XXXX-XXXX",
                Margin = new Thickness(0, 10, 0, 0),
                FontSize = 13,
                FontFamily = new FontFamily("Consolas")
            };

            var contentPanel = new StackPanel();

            // 텍스트 블록 생성
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = "라이선스 키를 입력하십시오.",
                FontSize = 12
            };
            // [핵심 해결 방법] XAML의 DynamicResource와 완벽하게 동일한 코드입니다.
            textBlock.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");

            contentPanel.Children.Add(textBlock);
            contentPanel.Children.Add(textBox);

            var dialog = new ContentDialog(DialogHost)
            {
                Title = "라이선스 정품 인증",
                Content = contentPanel,
                PrimaryButtonText = "인증하기",
                CloseButtonText = "취소",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string inputKey = textBox.Text.Trim();

                // [수정된 핵심 부분] Keygen.sh 서버에 키를 보내서 진짜인지 확인합니다.
                var validationResult = await Kys_cad_plugin.Core.LicenseManager.ValidateLicenseAsync(inputKey);

                if (validationResult.IsValid)
                {
                    // 서버 인증 통과 시 레지스트리에 저장하고 잠금 해제
                    Kys_cad_plugin.Core.RegistryHelper.SaveLicenseKey(inputKey);
                    CheckLicenseState();
                    System.Windows.MessageBox.Show("인증이 완료되었습니다. 플러그인을 정상적으로 사용할 수 있습니다.", "인증 성공");
                }
                else
                {
                    // 인증 실패 시 (기간 만료, 기기 불일치 등)
                    System.Windows.MessageBox.Show($"인증 실패: {validationResult.Message}\n(입력한 키: {inputKey})", "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        // [다이얼로그 2] 라이선스 정보 확인 창
        private async System.Threading.Tasks.Task ShowLicenseInfoDialogAsync(string currentKey)
        {
            // 전체 내용을 담을 메인 패널
            var mainPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            // 1. 정보를 담을 Grid
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });

            AddInfoRow(grid, 0, "KeySolid", "라이선스 키", MaskLicenseKey(currentKey));
            AddInfoRow(grid, 1, "CalendarRegular", "만료 일자", "2026. 12. 31");
            AddInfoRow(grid, 2, "ClockRegular", "남은 기간", "245 일 (활성 상태)");

            mainPanel.Children.Add(grid);

            // 2. 하단 여백 및 구분선 추가
            var separator = new System.Windows.Controls.Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 15, 0, 10)
            };
            separator.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "ControlElevationBorderBrush");
            mainPanel.Children.Add(separator);

            // 3. 편집 / 삭제 하이퍼링크 패널
            var linkPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            // [편집] 하이퍼링크
            var editLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("라이선스 변경"));
            editLink.TextDecorations = null; // 밑줄 제거
            editLink.Foreground = (Brush)new BrushConverter().ConvertFrom("#4A90E2"); // 세련된 파란색

            // [삭제] 하이퍼링크
            var deleteLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("기기에서 삭제"));
            deleteLink.TextDecorations = null; // 밑줄 제거
            deleteLink.Foreground = (Brush)new BrushConverter().ConvertFrom("#E74C3C"); // 경고용 빨간색

            // TextBlock에 하이퍼링크와 구분자(|) 추가
            var linkTextBlock = new System.Windows.Controls.TextBlock { FontSize = 12 };
            linkTextBlock.Inlines.Add(editLink);

            var divider = new System.Windows.Documents.Run("  |  ");
            // 구분선 색상은 다크 테마에 맞춰 동적으로 설정
            var dividerColor = new System.Windows.Controls.TextBlock();
            dividerColor.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
            divider.Foreground = dividerColor.Foreground; // 색상 복사
            linkTextBlock.Inlines.Add(divider);

            linkTextBlock.Inlines.Add(deleteLink);

            linkPanel.Children.Add(linkTextBlock);
            mainPanel.Children.Add(linkPanel);

            // 4. 다이얼로그 생성
            var dialog = new ContentDialog(DialogHost)
            {
                Title = "라이선스 정보",
                Content = mainPanel,
                CloseButtonText = "닫기"
            };

            // 5. 클릭 이벤트 처리 (다이얼로그를 닫고 이후 작업 수행)
            editLink.Click += async (s, e) =>
            {
                dialog.Hide(); // 현재 다이얼로그 닫기
                await ShowInputLicenseDialogAsync(); // 입력 창 띄우기
            };

            deleteLink.Click += (s, e) =>
            {
                dialog.Hide(); // 현재 다이얼로그 닫기
                var confirm = System.Windows.MessageBox.Show(
                    "정말 기기에서 라이선스를 삭제하시겠습니까?\n삭제 시 기능 사용이 제한됩니다.",
                    "라이선스 삭제",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirm == System.Windows.MessageBoxResult.Yes)
                {
                    Kys_cad_plugin.Core.RegistryHelper.DeleteLicenseKey();
                    CheckLicenseState();
                    System.Windows.MessageBox.Show("라이선스가 기기에서 삭제되었습니다.", "삭제 완료");
                }
            };

            await dialog.ShowAsync();
        }

        // 다이얼로그 내부의 정보 행을 이쁘게 만들어주는 헬퍼 메서드
        private void AddInfoRow(Grid grid, int row, string iconName, string label, string value)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // 아이콘 생성 및 다이내믹 컬러 적용
            var icon = new PackIconFontAwesome
            {
                Kind = (PackIconFontAwesomeKind)Enum.Parse(typeof(PackIconFontAwesomeKind), iconName),
                Width = 12,
                Height = 12,
                Margin = new Thickness(0, 0, 8, 0)
            };
            icon.SetResourceReference(PackIconFontAwesome.ForegroundProperty, "TextFillColorSecondaryBrush");
            sp.Children.Add(icon);

            // 라벨 생성 및 다이내믹 컬러 적용
            var labelBlock = new System.Windows.Controls.TextBlock
            {
                Text = label,
                FontSize = 12
            };
            labelBlock.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
            sp.Children.Add(labelBlock);

            Grid.SetRow(sp, row);
            Grid.SetColumn(sp, 0);
            grid.Children.Add(sp);

            // 값 (Value) 생성
            var valBlock = new System.Windows.Controls.TextBlock
            {
                Text = value,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(valBlock, row);
            Grid.SetColumn(valBlock, 1);
            grid.Children.Add(valBlock);
        }
        // 라이선스 키 일부 가리기 (보안)
        private string MaskLicenseKey(string key)
        {
            if (key.Length > 8)
                return key.Substring(0, 4) + "-XXXX-XXXX-" + key.Substring(key.Length - 4);
            return "XXXX-XXXX";
        }
    }
}