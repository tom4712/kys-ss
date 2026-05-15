using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Kys_cad_plugin.Views;

namespace Kys_cad_plugin.Core
{
    // 임포트 결과를 담을 클래스
    public class ImportResult
    {
        public List<Dictionary<string, string>> Rows { get; set; } = new List<Dictionary<string, string>>();
        public string FileName { get; set; }
        public int SuccessCount => Rows.Count;
    }

    public static class DataImportManager
    {
        /// <summary>
        /// 파일을 열고 유저에게 매핑을 받은 뒤 데이터를 반환하는 중앙 통로 메서드
        /// </summary>
        /// <param name="owner">다이얼로그를 띄울 부모 창</param>
        /// <param name="requiredFields">해당 기능에서 필요한 필드 목록 (예: "ID", "X", "Y")</param>
        public static async Task<ImportResult> ImportAndMap(Window owner, List<string> requiredFields)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "좌표 데이터 파일 (*.txt;*.csv)|*.txt;*.csv|모든 파일 (*.*)|*.*",
                Title = "데이터 파일 선택"
            };

            if (openFileDialog.ShowDialog() != true) return null;

            try
            {
                // 1. 파일 읽기
                string[] allLines = File.ReadAllLines(openFileDialog.FileName, Encoding.Default);
                var dataLines = allLines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

                if (dataLines.Count == 0) return null;

                // 2. 샘플 데이터(첫 줄) 추출
                string[] sampleRow = dataLines[0].Split(new char[] { ',', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // 3. 중앙 매핑 다이얼로그 호출 (분류 작업)
                var mappingDialog = new ColumnMappingDialog(sampleRow, requiredFields);
                mappingDialog.Owner = owner;

                if (mappingDialog.ShowDialog() != true) return null;

                // 4. 매핑 정보 및 시작 행 가져오기
                var indices = mappingDialog.MappedIndices;
                int startRow = mappingDialog.StartRow;
                int maxIdx = indices.Values.Max();

                var result = new ImportResult { FileName = openFileDialog.SafeFileName };

                // 5. 데이터 파싱 작업
                await Task.Run(() =>
                {
                    var rowsToProcess = dataLines.Skip(startRow - 1);
                    foreach (string line in rowsToProcess)
                    {
                        string[] parts = line.Split(new char[] { ',', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length <= maxIdx) continue;

                        var rowDict = new Dictionary<string, string>();
                        try
                        {
                            foreach (var field in requiredFields)
                            {
                                rowDict[field] = parts[indices[field]];
                            }
                            result.Rows.Add(rowDict);
                        }
                        catch { }
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"파일 읽기 중 오류 발생: {ex.Message}");
                return null;
            }
        }
    }
}