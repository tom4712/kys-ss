// 오토캐드 API 참조
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using Wpf.Ui.Controls;
using CadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Kys_cad_plugin.Views
{
    public class LayerExclusionItem
    {
        public string Name { get; set; }
        public bool IsExcluded { get; set; }
    }

    public partial class DupTextUI : FluentWindow
    {
        private ObservableCollection<LayerExclusionItem> _layerList = new ObservableCollection<LayerExclusionItem>();

        public DupTextUI()
        {
            InitializeComponent();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(this);

            LayerListView.ItemsSource = _layerList;
            LoadLayers();
        }

        private void BtnRefreshLayers_Click(object sender, RoutedEventArgs e)
        {
            LoadLayers();
        }

        private void LoadLayers()
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;

            _layerList.Clear();

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    foreach (ObjectId id in lt)
                    {
                        LayerTableRecord ltr = tr.GetObject(id, OpenMode.ForRead) as LayerTableRecord;
                        _layerList.Add(new LayerExclusionItem { Name = ltr.Name, IsExcluded = false });
                    }
                    tr.Commit();
                }

                var sorted = _layerList.OrderBy(x => x.Name).ToList();
                _layerList.Clear();
                foreach (var item in sorted) _layerList.Add(item);
            }
            catch { }
        }

        private async void BtnScanDup_Click(object sender, RoutedEventArgs e)
        {
            Document doc = CadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                TxtStatus.Text = "상태: 도면 스캔 중...";
                TxtDupList.Clear();

                HashSet<string> excludedLayers = _layerList
                    .Where(x => x.IsExcluded)
                    .Select(x => x.Name)
                    .ToHashSet();

                Dictionary<string, List<ObjectId>> textGroups = new Dictionary<string, List<ObjectId>>();

                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;

                        foreach (ObjectId objId in btr)
                        {
                            Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                            if (ent == null || excludedLayers.Contains(ent.Layer)) continue;

                            string content = null;
                            if (ent is DBText dbText) content = dbText.TextString.Trim();
                            else if (ent is MText mText) content = mText.Text.Trim();

                            if (!string.IsNullOrEmpty(content))
                            {
                                if (!textGroups.ContainsKey(content)) textGroups[content] = new List<ObjectId>();
                                textGroups[content].Add(objId);
                            }
                        }
                        tr.Commit();
                    }
                }

                var duplicates = textGroups.Where(g => g.Value.Count > 1).ToList();

                if (duplicates.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    List<ObjectId> idsToSelect = new List<ObjectId>();
                    Extents3d totalExtents = new Extents3d();
                    bool hasExtents = false;

                    // 사용자가 선택한 모드 가져오기 (0: 전체, 1: 1개 남기기, 2: 1개만 선택)
                    int selectMode = CboSelectMode.SelectedIndex;

                    foreach (var group in duplicates)
                    {
                        sb.AppendLine($"■ [중복 {group.Value.Count}건] : {group.Key}");

                        List<ObjectId> targetIdsForGroup = new List<ObjectId>();

                        if (selectMode == 0) // 모든 객체 선택
                        {
                            targetIdsForGroup = group.Value;
                        }
                        else if (selectMode == 1) // 1개 원본 보호, 나머지 선택 (가장 많이 씀)
                        {
                            // 첫 번째 요소를 제외(Skip(1))하고 나머지만 리스트업
                            targetIdsForGroup = group.Value.Skip(1).ToList();
                        }
                        else if (selectMode == 2) // 그룹당 1개만 선택
                        {
                            targetIdsForGroup = new List<ObjectId> { group.Value.First() };
                        }

                        foreach (ObjectId id in targetIdsForGroup)
                        {
                            idsToSelect.Add(id);

                            using (Transaction tr = db.TransactionManager.StartTransaction())
                            {
                                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                                if (ent != null && ent.Bounds.HasValue)
                                {
                                    if (!hasExtents)
                                    {
                                        totalExtents = ent.Bounds.Value;
                                        hasExtents = true;
                                    }
                                    else
                                    {
                                        totalExtents.AddExtents(ent.Bounds.Value);
                                    }
                                }
                                tr.Commit();
                            }
                        }
                    }

                    TxtDupList.Text = sb.ToString();
                    TxtCount.Text = idsToSelect.Count.ToString();
                    TxtStatus.Text = "상태: 검사 완료 (중복 발견)";

                    ed.SetImpliedSelection(idsToSelect.ToArray());

                    if (hasExtents)
                    {
                        ed.UpdateScreen();
                        ViewTableRecord view = ed.GetCurrentView();

                        double width = totalExtents.MaxPoint.X - totalExtents.MinPoint.X;
                        double height = totalExtents.MaxPoint.Y - totalExtents.MinPoint.Y;

                        if (width == 0) width = 100;
                        if (height == 0) height = 100;

                        Point2d center = new Point2d((totalExtents.MaxPoint.X + totalExtents.MinPoint.X) / 2.0,
                                                     (totalExtents.MaxPoint.Y + totalExtents.MinPoint.Y) / 2.0);

                        view.CenterPoint = center;
                        view.Width = width * 1.3;
                        view.Height = height * 1.3;
                        ed.SetCurrentView(view);
                    }

                    this.WindowState = WindowState.Minimized;
                    ed.WriteMessage($"\n▶ {idsToSelect.Count}개의 중복 관련 객체가 선택되었습니다.");
                }
                else
                {
                    TxtStatus.Text = "상태: 검사 완료 (중복 없음)";
                    TxtCount.Text = "0";
                    ed.SetImpliedSelection(new ObjectId[0]);
                    await ShowModernDialog("알림", "선택한 도면층 조건에서 중복된 텍스트가 발견되지 않았습니다.");
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "상태: 오류 발생";
                await ShowModernDialog("오류", ex.Message);
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