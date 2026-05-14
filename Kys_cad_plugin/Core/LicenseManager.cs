using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;

namespace Kys_cad_plugin.Core
{

    public class LicenseInfo
    {
        public string Key { get; set; }
        public string Name { get; set; }        // 라이선스 이름
        public string Status { get; set; }
        public DateTime? Expiry { get; set; }
        public bool IsValid { get; set; }
        public int MaxMachines { get; set; }    // 허용된 최대 기기 수
        public int CurrentMachines { get; set; } // 현재 등록된 기기 수
        public string MachineId { get; set; }   // 기기 삭제(Deactivate)용
        public string Detail { get; set; }
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

                // ====================================================================
                // ⭐️ [초과 등록 원천 차단] 기기를 등록(Activate)하기 전에, 대수가 꽉 찼는지 확인!
                var info = await GetFullLicenseInfoAsync(licenseKey);

                if (info != null && info.CurrentMachines >= info.MaxMachines)
                {
                    return (false, $"[인증 실패] 허용된 기기 대수({info.MaxMachines}대)가 꽉 찼습니다.\n다른 PC에서 등록 해제 후 시도하세요.");
                }
                // ====================================================================

                // 여유가 있을 때만 기기 활성화 실행
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
            string fingerprint = GetMachineFingerprint().Trim();
            string currentMachineId = "";
            int current = 0;
            int max = 1;

            // 1단계: 기존처럼 validate-key를 통해 라이선스 기본 정보와 'License ID'를 가져옵니다.
            string url = $"https://api.keygen.sh/v1/accounts/{ACCOUNT_ID}/licenses/actions/validate-key?include=policy";
            var requestData = new { meta = new { key = key, scope = new { fingerprint = fingerprint } } };

            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Accept", MEDIA_TYPE);
                request.Headers.Add("Keygen-Version", API_VERSION);
                request.Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, MEDIA_TYPE);

                try
                {
                    var response = await client.SendAsync(request);
                    var json = await response.Content.ReadAsStringAsync();

                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        if (!doc.RootElement.TryGetProperty("data", out var data)) return null;

                        var meta = doc.RootElement.GetProperty("meta");
                        var attr = data.GetProperty("attributes");
                        var rels = data.GetProperty("relationships");

                        // ⭐️ 여기서 라이선스 고유 ID를 빼냅니다.
                        string licenseId = data.GetProperty("id").GetString();

                        // 현재 등록된 기기 수 파싱
                        if (rels.TryGetProperty("machines", out var machines) && machines.TryGetProperty("meta", out var mMeta))
                            current = mMeta.GetProperty("count").GetInt32();

                        // 최대 허용 대수 파싱
                        if (attr.TryGetProperty("maxMachines", out var maxAttr) && maxAttr.ValueKind != JsonValueKind.Null)
                            max = maxAttr.GetInt32();
                        else if (doc.RootElement.TryGetProperty("included", out var included))
                        {
                            foreach (var item in included.EnumerateArray())
                            {
                                if (item.GetProperty("type").GetString() == "policies")
                                {
                                    max = item.GetProperty("attributes").GetProperty("maxMachines").GetInt32();
                                    break;
                                }
                            }
                        }

                        // ====================================================================
                        // ⭐️ [문제 해결의 핵심] Keygen 전용 기기 목록 API(GET)를 직접 찔러서 기기 ID를 빼옵니다!
                        // ====================================================================
                        string machinesUrl = $"https://api.keygen.sh/v1/accounts/{ACCOUNT_ID}/licenses/{licenseId}/machines";
                        var machReq = new HttpRequestMessage(HttpMethod.Get, machinesUrl);
                        machReq.Headers.Add("Accept", MEDIA_TYPE);
                        machReq.Headers.Add("Keygen-Version", API_VERSION);
                        machReq.Headers.Authorization = new AuthenticationHeaderValue("License", key); // 이 요청엔 키가 필요함

                        var machRes = await client.SendAsync(machReq);
                        if (machRes.IsSuccessStatusCode)
                        {
                            var machJson = await machRes.Content.ReadAsStringAsync();
                            using (JsonDocument machDoc = JsonDocument.Parse(machJson))
                            {
                                if (machDoc.RootElement.TryGetProperty("data", out var machData) && machData.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var item in machData.EnumerateArray())
                                    {
                                        if (item.GetProperty("attributes").TryGetProperty("fingerprint", out var fpProp))
                                        {
                                            string serverFingerprint = fpProp.GetString() ?? "";
                                            // 대소문자 무시하고 내 기기 지문과 완벽 매칭
                                            if (string.Equals(serverFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
                                            {
                                                currentMachineId = item.GetProperty("id").GetString();
                                                break; // 내 기기 ID 찾았으니 종료
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        // ====================================================================

                        return new LicenseInfo
                        {
                            Key = key,
                            Name = attr.TryGetProperty("name", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : "사용자명 없음",
                            Status = attr.GetProperty("status").GetString(),
                            Expiry = attr.TryGetProperty("expiry", out var e) && e.ValueKind != JsonValueKind.Null ? e.GetDateTime().ToLocalTime() : (DateTime?)null,
                            IsValid = meta.GetProperty("valid").GetBoolean(),
                            Detail = meta.TryGetProperty("detail", out var d) ? d.GetString() : "",
                            MachineId = currentMachineId,  // 이제 절대 빈 값("")이 들어가지 않습니다.
                            MaxMachines = max,
                            CurrentMachines = current
                        };
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public static async Task<(bool Success, string Message)> DeactivateMachineAsync(string key, string machineId)
        {
            if (string.IsNullOrEmpty(machineId))
                return (false, "삭제할 기기 ID를 찾을 수 없습니다.");

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

                    if (response.IsSuccessStatusCode)
                        return (true, "기기 등록 해제 성공");

                    return (false, $"서버 삭제 실패 (HTTP {response.StatusCode})");
                }
                catch (Exception ex)
                {
                    return (false, $"통신 오류: {ex.Message}");
                }
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

        public static async Task<(bool Success, string Message)> ActivateMachineAsync(string key, string licenseId, string fingerprint)
        {

            string url = $"https://api.keygen.sh/v1/accounts/{ACCOUNT_ID}/machines";
            var requestData = new { data = new { type = "machines", attributes = new { fingerprint = fingerprint, name = Environment.MachineName }, relationships = new { license = new { data = new { type = "licenses", id = licenseId } } } } };

            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Accept", MEDIA_TYPE);
                request.Headers.Add("Keygen-Version", API_VERSION);
                request.Headers.Authorization = new AuthenticationHeaderValue("License", key);
                request.Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, MEDIA_TYPE);

                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                // 등록 성공
                if (response.IsSuccessStatusCode) return (true, "기기 등록 성공");

                // ⭐️ 등록 실패 시 (서버 거절 사유 정밀 분석)
                using (JsonDocument doc = JsonDocument.Parse(responseBody))
                {
                    if (doc.RootElement.TryGetProperty("errors", out var errors))
                    {
                        var err = errors[0];
                        string code = err.GetProperty("code").GetString();
                        string detail = err.GetProperty("detail").GetString();

                        // 서버에서 보낸 "한도 초과" 코드 차단
                        if (code == "MACHINE_LIMIT_REACHED" || detail.Contains("limit reached"))
                        {
                            return (false, "허용된 기기 대수가 초과되었습니다. 다른 PC에서 인증을 해제하세요.");
                        }
                        if (code == "FINGERPRINT_TAKEN")
                        {
                            return (false, "이미 등록된 기기입니다.");
                        }
                        return (false, $"인증 오류: {detail}");
                    }
                }
                return (false, $"인증 오류 ({response.StatusCode})");
            }
        }
    }
}