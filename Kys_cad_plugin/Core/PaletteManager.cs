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
                // [1] 새로운 GUID로 팔레트 생성 (매번 위치 초기화 효과)
                _ps = new PaletteSet("KYSQL 도구함", Guid.NewGuid());

                // [2] 기본 스타일 설정 (닫기, 자동숨김, 속성메뉴)
                _ps.Style = PaletteSetStyles.ShowCloseButton |
                            PaletteSetStyles.ShowAutoHideButton |
                            PaletteSetStyles.ShowPropertiesMenu;

                // [3] ★ 중요: DockSides.None이 반드시 포함되어야 나중에 뗄 수 있습니다.
                _ps.DockEnabled = DockSides.Right | DockSides.Left | DockSides.None;

                // [4] 크기 설정
                _ps.MinimumSize = new Size(50, 400);
                _ps.Size = new Size(300, 600);

                // [5] 컨트롤 추가
                var control = new MainPaletteControl();
                _ps.AddVisual("도구", control);

                // [6] ★ 핵심 수정: 먼저 Visible을 true로 만들어서 팔레트를 활성화한 후, 
                // 그 직후에 Dock 위치를 지정해야 오토캐드가 도킹 명령을 정확히 수행합니다.
                _ps.Visible = true;
                _ps.Dock = DockSides.Right;
            }
            else
            {
                // 이미 생성된 적이 있다면 위치를 강제하지 않고 가시성만 켭니다.
                // (사용자가 이전에 떼어놓았다면 그 위치를 유지하게 됩니다.)
                _ps.Visible = true;
            }
        }
    }
}