using System;
using System.Drawing; // Size 객체 사용을 위해 필요
using Autodesk.AutoCAD.Windows;
using Kys_cad_plugin.Views;

namespace Kys_cad_plugin.Core
{
    public static class PaletteManager
    {
        private static PaletteSet? _ps = null;

        public static void Show()
        {
            if (_ps == null)
            {
                // [수정] 실행할 때마다 랜덤 GUID를 생성하여 오토캐드의 '기억'을 완전히 초기화합니다.
                _ps = new PaletteSet("KYSQL 도구함", Guid.NewGuid());

                _ps.Style = PaletteSetStyles.ShowCloseButton |
                            PaletteSetStyles.ShowAutoHideButton |
                            PaletteSetStyles.ShowPropertiesMenu;

                _ps.DockEnabled = DockSides.Right | DockSides.Left;

                // 최소 너비를 아주 작게 설정 (나중에 마우스로 자유롭게 조절 가능)
                _ps.MinimumSize = new Size(50, 400);

                // 초기 실행 시 너비를 200으로 슬림하게 고정
                _ps.Size = new Size(200, 600);

                var control = new MainPaletteControl();
                _ps.AddVisual("도구", control);
            }

            _ps.Visible = true;
            _ps.Dock = DockSides.Right;
        }
    }
}