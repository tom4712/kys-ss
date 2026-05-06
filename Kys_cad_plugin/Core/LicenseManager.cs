using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kys_cad_plugin.Core
{

    public class LicenseInfo
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public DateTime? Expiry { get; set; }
        public string Detail { get; set; }
        public bool IsValid { get; set; }

        // 추가된 필드
        public int MaxMachines { get; set; }
        public int CurrentMachines { get; set; }
        public string MachineId { get; set; } // 서버에서 이 기기를 삭제할 때 필요한 UUID
    }
    public static class LicenseManager
    {
        // 1. 이미지에서 확인된 Account UUID를 사용합니다 (Slug의 빨간 삼각형 경고 회피)
        private const string ACCOUNT_ID = "d59ef884-22eb-46cf-bf1c-a07b7a8bd1f2";
        private const string API_VERSION = "1.8";
        private const string MEDIA_TYPE = "application/vnd.api+json";

        public static string GetMachineFingerprint()
        {
            var macAddr = (from nic in NetworkInterface.GetAllNetworkInterfaces()
                           where nic.OperationalStatus == OperationalStatus.Up
                           select nic.GetPhysicalAddress().ToString()
                          ).FirstOrDefault();
            return string.IsNullOrEmpty(macAddr) ? "UNKNOWN-MACHINE" : macAddr;
        }

        public static async Task<(bool IsValid, string Message)> ValidateLicenseAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey)) return (false, "키를 입력해주세요.");

            licenseKey = licenseKey.Trim();
            string fingerprint = GetMachineFingerprint();

            // STEP 1: 검증 및 라이선스 UUID(ID) 확보
            var (valid, detail, licenseId) = await RequestValidationAsync(licenseKey, fingerprint);

            if (valid) return (true, "인증 성공");

            // STEP 2: 기기 등록이 안 된 경우 자동 등록 진행
            if (detail.Contains("fingerprint is not activated") || detail.Contains("no associated machine"))
            {
                if (string.IsNullOrEmpty(licenseId))
                    return (false, "라이선스 정보를 가져올 수 없습니다. 정책 설정을 확인하세요.");

                // 기기 활성화 실행
                var activation = await ActivateMachineAsync(licenseKey, licenseId, fingerprint);

                if (activation.Success)
                {
                    // 등록 성공 후 최종 재검증
                    var (finalValid, finalDetail, _) = await RequestValidationAsync(licenseKey, fingerprint);
                    return (finalValid, finalValid ? "인증 성공 (기기 활성화 완료)" : finalDetail);
                }

                return (false, $"기기 등록 실패: {activation.Message}");
            }

            return (false, $"인증 실패: {detail}");
        }


        public static async Task<LicenseInfo> GetFullLicenseInfoAsync(string key)
        {
            string fingerprint = GetMachineFingerprint();
            // 설정을 실시간으로 반영하기 위해 policy 정보를 포함해서 요청합니다 (?include=policy)
            string url = $"https://api.keygen.sh/v1/accounts/{ACCOUNT_ID}/licenses/actions/validate-key?include=policy";
            var requestData = new { meta = new { key = key, scope = new { fingerprint = fingerprint } } };

            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Accept", MEDIA_TYPE);
                request.Headers.Add("Keygen-Version", API_VERSION);
                var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, MEDIA_TYPE);
                request.Content = content;

                var response = await client.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (!doc.RootElement.TryGetProperty("data", out var data)) return null;

                    var meta = doc.RootElement.GetProperty("meta");
                    var attr = data.GetProperty("attributes");
                    var rels = data.GetProperty("relationships");

                    // 1. 현재 등록된 기기 수 (relationships -> machines -> meta -> count)
                    int current = 0;
                    if (rels.TryGetProperty("machines", out var machines) &&
                        machines.TryGetProperty("meta", out var mMeta))
                    {
                        current = mMeta.GetProperty("count").GetInt32();
                    }

                    // 2. 최대 허용 기기 수 (Policy 또는 License Override 확인)
                    int max = 1;
                    // 우선 라이선스 자체에 설정된 override가 있는지 확인
                    if (attr.TryGetProperty("maxMachines", out var maxAttr) && maxAttr.ValueKind != JsonValueKind.Null)
                    {
                        max = maxAttr.GetInt32();
                    }
                    else if (doc.RootElement.TryGetProperty("included", out var included))
                    {
                        // 포함된 Policy 데이터에서 maxMachines를 찾아옴
                        foreach (var item in included.EnumerateArray())
                        {
                            if (item.GetProperty("type").GetString() == "policies")
                            {
                                max = item.GetProperty("attributes").GetProperty("maxMachines").GetInt32();
                            }
                        }
                    }

                    // 현재 PC의 Machine UUID 찾기 (삭제용)
                    string currentMachineId = "";
                    if (doc.RootElement.TryGetProperty("included", out var incl))
                    {
                        foreach (var item in incl.EnumerateArray())
                        {
                            if (item.GetProperty("type").GetString() == "machines" &&
                                item.GetProperty("attributes").GetProperty("fingerprint").GetString() == fingerprint)
                            {
                                currentMachineId = item.GetProperty("id").GetString();
                            }
                        }
                    }

                    return new LicenseInfo
                    {
                        Key = key,
                        // [핵심 추가] 라이선스 이름 (data.attributes.name) 추출
                        Name = attr.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "이름 없음",
                        Status = attr.GetProperty("status").GetString(),
                        Expiry = attr.TryGetProperty("expiry", out var exp) && exp.ValueKind != JsonValueKind.Null ? exp.GetDateTime().ToLocalTime() : (DateTime?)null,
                        IsValid = meta.GetProperty("valid").GetBoolean(),
                        Detail = meta.GetProperty("detail").GetString(),
                        MachineId = currentMachineId,
                        MaxMachines = max,
                        CurrentMachines = current
                    };
                }
            }
        }

        public static async Task<(bool Success, string Message)> DeactivateMachineAsync(string key, string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return (false, "삭제할 기기 ID가 없습니다.");

            string url = $"https://api.keygen.sh/v1/accounts/{ACCOUNT_ID}/machines/{machineId}";

            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, url);
                request.Headers.Add("Accept", MEDIA_TYPE);
                request.Headers.Add("Keygen-Version", API_VERSION);
                request.Headers.Authorization = new AuthenticationHeaderValue("License", key);

                try
                {
                    var response = await client.SendAsync(request);
                    if (response.IsSuccessStatusCode) return (true, "서버 기기 삭제 성공");

                    return (false, $"서버 삭제 실패 (코드: {response.StatusCode})");
                }
                catch (Exception ex) { return (false, ex.Message); }
            }
        }

        private static async Task<(bool Valid, string Detail, string LicenseId)> RequestValidationAsync(string key, string fingerprint)
        {
            string url = $"https://api.keygen.sh/v1/accounts/{ACCOUNT_ID}/licenses/actions/validate-key";
            var requestData = new { meta = new { key = key, scope = new { fingerprint = fingerprint } } };

            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Accept", MEDIA_TYPE);
                request.Headers.Add("Keygen-Version", API_VERSION);

                var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8);
                content.Headers.ContentType = new MediaTypeHeaderValue(MEDIA_TYPE);
                request.Content = content;

                var response = await client.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    var meta = doc.RootElement.GetProperty("meta");
                    bool isValid = meta.GetProperty("valid").GetBoolean();
                    string detail = meta.GetProperty("detail").GetString() ?? "";

                    string licId = "";
                    if (doc.RootElement.TryGetProperty("data", out var data))
                        licId = data.GetProperty("id").GetString();

                    return (isValid, detail, licId);
                }
            }
        }

        private static async Task<(bool Success, string Message)> ActivateMachineAsync(string key, string licenseId, string fingerprint)
        {
            // [URL] Account UUID를 사용하여 경로를 확실히 잡습니다.
            string url = $"https://api.keygen.sh/v1/accounts/{ACCOUNT_ID}/machines";

            var requestData = new
            {
                data = new
                {
                    type = "machines",
                    attributes = new
                    {
                        fingerprint = fingerprint,
                        name = Environment.MachineName
                    },
                    relationships = new
                    {
                        license = new
                        {
                            data = new { type = "licenses", id = licenseId }
                        }
                    }
                }
            };

            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);

                // [헤더 필수 설정]
                request.Headers.Clear();
                request.Headers.Add("Accept", MEDIA_TYPE);
                request.Headers.Add("Keygen-Version", API_VERSION);

                // [가장 중요한 수정] v1.8에서 라이선스 키는 "License" 스키마를 사용해야 합니다.
                // Authorization: License MWMM-3TXR-...
                request.Headers.Authorization = new AuthenticationHeaderValue("License", key);

                var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8);
                content.Headers.ContentType = new MediaTypeHeaderValue(MEDIA_TYPE);
                request.Content = content;

                try
                {
                    var response = await client.SendAsync(request);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode) return (true, "성공");

                    // 서버가 보낸 구체적인 에러 메시지 분석
                    using (JsonDocument doc = JsonDocument.Parse(responseBody))
                    {
                        if (doc.RootElement.TryGetProperty("errors", out var errors))
                        {
                            string detail = errors[0].GetProperty("detail").GetString();
                            return (false, detail);
                        }
                    }
                    return (false, $"인증 오류 ({response.StatusCode})");
                }
                catch (Exception ex)
                {
                    return (false, $"연결 실패: {ex.Message}");
                }
            }
        }
    }
}