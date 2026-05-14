using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Kys_cad_plugin.Core;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

[assembly: ExtensionApplication(typeof(Kys_cad_plugin.AppInitializer))]

namespace Kys_cad_plugin
{
    public class AppInitializer : IExtensionApplication
    {
        public void Initialize()
        {
            string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

            // [수정] 버전과 상관없이 우리 폴더에 있는 DLL을 우선적으로 연결합니다.
            AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
            {
                // 요청된 DLL 파일명을 생성 (예: MahApps.Metro.IconPacks.Core.dll)
                string dllPath = Path.Combine(pluginDirectory, $"{assemblyName.Name}.dll");

                if (File.Exists(dllPath))
                {
                    // [핵심] 여기서 로드하면 요청된 버전(6.0.0.0)이 실제 파일 버전(6.2.1.0)과 
                    // 달라도 우리 파일을 대신 사용하게 됩니다.
                    return context.LoadFromAssemblyPath(dllPath);
                }
                return null;
            };

            Application.Idle += (s, e) =>
            {
                Application.Idle -= (EventHandler)s!;
                try
                {
                    // 여기서 라이선스 검사 금지! 무조건 UI를 먼저 호출합니다.
                    PaletteManager.Show();
                }
                catch { }
            };
        }

        public void Terminate() { }
    }
}