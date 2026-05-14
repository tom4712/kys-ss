// 오토캐드 API 참조
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    public partial class TxtCollectUI : FluentWindow
    {
        private ObservableCollection<CollectedTextData> _collectedDataList = new ObservableCollection<CollectedTextData>();

        public TxtCollectUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);
            CollectListView.ItemsSource = _collectedDataList;
        }

        // [기존 수집 로직 유지] 도면에서 텍스트/다중텍스트 수집
        private void BtnCollect_Click(object sender, RoutedEventArgs e)
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            this.WindowState = WindowState.Minimized;

            try
            {
                TypedValue[] tvs = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<OR"),
                    new TypedValue((int)DxfCode.Start, "TEXT"),
                    new TypedValue((int)DxfCode.Start, "MTEXT"),
                    new TypedValue((int)DxfCode.Operator, "OR>")
                };
                SelectionFilter filter = new SelectionFilter(tvs);

                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n수집할 텍스트 영역을 드래그하세요 (완료 시 Enter): ";

                PromptSelectionResult psr = ed.GetSelection(pso, filter);

                if (psr.Status == PromptStatus.OK && psr.Value != null)
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        _collectedDataList.Clear();
                        int count = 0;

                        foreach (SelectedObject so in psr.Value)
                        {
                            Entity ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;

                            if (ent is DBText dbText)
                            {
                                _collectedDataList.Add(new CollectedTextData
                                {
                                    Text = dbText.TextString,
                                    X = Math.Round(dbText.Position.X, 3),
                                    Y = Math.Round(dbText.Position.Y, 3),
                                    Z = Math.Round(dbText.Position.Z, 3),
                                    LayerName = dbText.Layer
                                });
                                count++;
                            }
                            else if (ent is MText mText)
                            {
                                _collectedDataList.Add(new CollectedTextData
                                {
                                    Text = mText.Contents,
                                    X = Math.Round(mText.Location.X, 3),
                                    Y = Math.Round(mText.Location.Y, 3),
                                    Z = Math.Round(mText.Location.Z, 3),
                                    LayerName = mText.Layer
                                });
                                count++;
                            }
                        }
                        TxtCollectedCount.Text = count.ToString();
                        tr.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"데이터 수집 오류: {ex.Message}");
            }
            finally
            {
                this.WindowState = WindowState.Normal;
                this.Activate();
            }
        }

        // [변경 로직] 엑셀 대신 텍스트 파일로 저장
        private async void BtnSaveTxt_Click(object sender, RoutedEventArgs e)
        {
            if (_collectedDataList.Count == 0)
            {
                await ShowModernDialog("알림", "저장할 데이터가 없습니다. 먼저 수집해 주세요.");
                return;
            }

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "텍스트 파일 (*.txt)|*.txt|CSV 파일 (*.csv)|*.csv",
                FileName = $"CollectedData_{DateTime.Now:yyyyMMdd_HHmm}",
                Title = "데이터 저장 위치 선택"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    // 구분자 설정 (CSV면 쉼표, TXT면 탭)
                    string separator = saveDialog.FileName.EndsWith(".csv") ? "," : "\t";

                    using (FileStream fs = new FileStream(saveDialog.FileName, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    using (StreamWriter sw = new StreamWriter(fs, Encoding.UTF8))
                    {
                        // 1. 헤더 작성
                        List<string> headers = new List<string>();
                        if (ChkText.IsChecked == true) headers.Add("Text");
                        if (ChkX.IsChecked == true) headers.Add("X");
                        if (ChkY.IsChecked == true) headers.Add("Y");
                        if (ChkZ.IsChecked == true) headers.Add("Z");
                        if (ChkLayer.IsChecked == true) headers.Add("Layer");

                        sw.WriteLine(string.Join(separator, headers));

                        // 2. 데이터 작성
                        foreach (var data in _collectedDataList)
                        {
                            List<string> row = new List<string>();
                            if (ChkText.IsChecked == true) row.Add(data.Text);
                            if (ChkX.IsChecked == true) row.Add(data.X.ToString());
                            if (ChkY.IsChecked == true) row.Add(data.Y.ToString());
                            if (ChkZ.IsChecked == true) row.Add(data.Z.ToString());
                            if (ChkLayer.IsChecked == true) row.Add(data.LayerName);

                            sw.WriteLine(string.Join(separator, row));
                        }
                    }

                    await ShowModernDialog("성공", $"{_collectedDataList.Count}개의 데이터를 파일로 저장했습니다.");
                }
                catch (Exception ex)
                {
                    await ShowModernDialog("오류", $"파일 저장 중 오류가 발생했습니다: {ex.Message}");
                }
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
                Width = 400,
                Height = 200
            };
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(msgBox);
            await msgBox.ShowDialogAsync();
        }
    }
}