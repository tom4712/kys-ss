using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kys_cad_plugin.Core
{
    internal class LicenseInfo
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public DateTime? Expiry { get; set; }
        public bool IsValid { get; set; }
        public int MaxMachines { get; set; }
        public int CurrentMachines { get; set; }
        public string MachineId { get; set; }
        public string Detail { get; set; }
    }

    internal static class LicenseManager
    {
        private static readonly string ACCOUNT_ID = "d59ef884-22eb-46cf-bf1c-a07b7a8bd1f2";
        private static readonly string API_VERSION = "1.8";
        private static readonly string MEDIA_TYPE = "application/vnd.api+json";

        internal static string GetMachineFingerprint()
        {
            var macAddr = (from nic in NetworkInterface.GetAllNetworkInterfaces()
                           where nic.OperationalStatus == OperationalStatus.Up
                           select nic.GetPhysicalAddress().ToString()
                          ).FirstOrDefault();
            return string.IsNullOrEmpty(macAddr) ? "UNKNOWN-MACHINE" : macAddr;
        }

        internal static async Task<(bool IsValid, string Message)> ValidateLicenseAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey)) return (false, "키를 입력해주세요.");

            licenseKey = licenseKey.Trim();
            string fingerprint = GetMachineFingerprint();

            var (valid, detail, licenseId) = await RequestValidationAsync(licenseKey, fingerprint);

            if (valid) return (true, "인증 성공");

            if (detail.Contains("fingerprint is not activated") || detail.Contains("no associated machine"))
            {
                if (string.IsNullOrEmpty(licenseId))
                    return (false, "라이선스 정보를 가져올 수 없습니다. 정책 설정을 확인하세요.");

                var info = await GetFullLicenseInfoAsync(licenseKey);

                if (info != null && info.CurrentMachines >= info.MaxMachines)
                {
                    return (false, $"[인증 실패] 허용된 기기 대수({info.MaxMachines}대)가 꽉 찼습니다.\n다른 PC에서 등록 해제 후 시도하세요.");
                }

                var activation = await ActivateMachineAsync(licenseKey, licenseId, fingerprint);

                if (activation.Success)
                {
                    var (finalValid, finalDetail, _) = await RequestValidationAsync(licenseKey, fingerprint);
                    return (finalValid, finalValid ? "인증 성공 (기기 활성화 완료)" : finalDetail);
                }

                return (false, $"기기 등록 실패: {activation.Message}");
            }

            return (false, $"인증 실패: {detail}");
        }

        internal static async Task<LicenseInfo> GetFullLicenseInfoAsync(string key)
        {
            string fingerprint = GetMachineFingerprint().Trim();
            string currentMachineId = "";
            int current = 0;
            int max = 1;

            string url = $"https://api.keygen.sh/v1/accounts/{ACCOUNT_ID}/licenses/actions/validate-key?include=policy";

            // 💡 [핵심 해결] 난독화에 파괴되지 않는 Dictionary 방식
            var requestData = new Dictionary<string, object>
            {
                ["meta"] = new Dictionary<string, object>
                {
                    ["key"] = key,
                    ["scope"] = new Dictionary<string, object> { ["fingerprint"] = fingerprint }
                }
            };

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

                        // 💡 오토캐드 튕김 방지를 위한 예외 처리 (TryGetProperty)
                        if (!doc.RootElement.TryGetProperty("meta", out var meta)) return null;
                        if (!data.TryGetProperty("attributes", out var attr)) return null;

                        var rels = data.TryGetProperty("relationships", out var r) ? r : default;

                        string licenseId = data.TryGetProperty("id", out var id) ? id.GetString() : "";

                        if (rels.ValueKind != JsonValueKind.Undefined && rels.TryGetProperty("machines", out var machines) && machines.TryGetProperty("meta", out var mMeta))
                            current = mMeta.GetProperty("count").GetInt32();

                        if (attr.TryGetProperty("maxMachines", out var maxAttr) && maxAttr.ValueKind != JsonValueKind.Null)
                            max = maxAttr.GetInt32();
                        else if (doc.RootElement.TryGetProperty("included", out var included))
                        {
                            foreach (var item in included.EnumerateArray())
                            {
                                if (item.TryGetProperty("type", out var type) && type.GetString() == "policies")
                                {
                                    max = item.GetProperty("attributes").GetProperty("maxMachines").GetInt32();
                                    break;
                                }
                            }
                        }

                        string machinesUrl = $"https://api.keygen.sh/v1/accounts/{ACCOUNT_ID}/licenses/{licenseId}/machines";
                        var machReq = new HttpRequestMessage(HttpMethod.Get, machinesUrl);
                        machReq.Headers.Add("Accept", MEDIA_TYPE);
                        machReq.Headers.Add("Keygen-Version", API_VERSION);
                        machReq.Headers.Authorization = new AuthenticationHeaderValue("License", key);

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
                                            if (string.Equals(serverFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
                                            {
                                                currentMachineId = item.GetProperty("id").GetString();
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        return new LicenseInfo
                        {
                            Key = key,
                            Name = attr.TryGetProperty("name", out var n) && n.ValueKind != JsonValueKind.Null ? n.GetString() : "사용자명 없음",
                            Status = attr.TryGetProperty("status", out var st) ? st.GetString() : "",
                            Expiry = attr.TryGetProperty("expiry", out var e) && e.ValueKind != JsonValueKind.Null ? e.GetDateTime().ToLocalTime() : (DateTime?)null,
                            IsValid = meta.TryGetProperty("valid", out var v) && v.GetBoolean(),
                            Detail = meta.TryGetProperty("detail", out var d) ? d.GetString() : "",
                            MachineId = currentMachineId,
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

        internal static async Task<(bool Success, string Message)> DeactivateMachineAsync(string key, string machineId)
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

            // 💡 [핵심 해결] 난독화에 파괴되지 않는 Dictionary 방식
            var requestData = new Dictionary<string, object>
            {
                ["meta"] = new Dictionary<string, object>
                {
                    ["key"] = key,
                    ["scope"] = new Dictionary<string, object> { ["fingerprint"] = fingerprint }
                }
            };

            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Accept", MEDIA_TYPE);
                request.Headers.Add("Keygen-Version", API_VERSION);

                var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8);
                content.Headers.ContentType = new MediaTypeHeaderValue(MEDIA_TYPE);
                request.Content = content;

                try
                {
                    var response = await client.SendAsync(request);
                    var json = await response.Content.ReadAsStringAsync();

                    using (JsonDocument doc = JsonDocument.Parse(json))
                    {
                        // 💡 오토캐드 튕김 방지 (서버가 에러를 리턴했을 때 즉사 방지)
                        if (!doc.RootElement.TryGetProperty("meta", out var meta))
                        {
                            return (false, "서버 응답 오류 (meta 데이터 없음)", "");
                        }

                        bool isValid = meta.TryGetProperty("valid", out var v) && v.GetBoolean();
                        string detail = meta.TryGetProperty("detail", out var d) ? d.GetString() : "";

                        string licId = "";
                        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null)
                            licId = data.TryGetProperty("id", out var id) ? id.GetString() : "";

                        return (isValid, detail, licId);
                    }
                }
                catch (Exception ex)
                {
                    return (false, $"네트워크 에러: {ex.Message}", "");
                }
            }
        }

        internal static async Task<(bool Success, string Message)> ActivateMachineAsync(string key, string licenseId, string fingerprint)
        {
            string url = $"https://api.keygen.sh/v1/accounts/{ACCOUNT_ID}/machines";

            // 💡 [핵심 해결] 난독화에 파괴되지 않는 Dictionary 방식
            var requestData = new Dictionary<string, object>
            {
                ["data"] = new Dictionary<string, object>
                {
                    ["type"] = "machines",
                    ["attributes"] = new Dictionary<string, object>
                    {
                        ["fingerprint"] = fingerprint,
                        ["name"] = Environment.MachineName
                    },
                    ["relationships"] = new Dictionary<string, object>
                    {
                        ["license"] = new Dictionary<string, object>
                        {
                            ["data"] = new Dictionary<string, object>
                            {
                                ["type"] = "licenses",
                                ["id"] = licenseId
                            }
                        }
                    }
                }
            };

            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Accept", MEDIA_TYPE);
                request.Headers.Add("Keygen-Version", API_VERSION);
                request.Headers.Authorization = new AuthenticationHeaderValue("License", key);
                request.Content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, MEDIA_TYPE);

                try
                {
                    var response = await client.SendAsync(request);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode) return (true, "기기 등록 성공");

                    using (JsonDocument doc = JsonDocument.Parse(responseBody))
                    {
                        if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
                        {
                            var err = errors[0];
                            string code = err.TryGetProperty("code", out var c) ? c.GetString() : "";
                            string detail = err.TryGetProperty("detail", out var d) ? d.GetString() : "";

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
                catch (Exception ex)
                {
                    return (false, $"네트워크 에러: {ex.Message}");
                }
            }
        }
    }
}