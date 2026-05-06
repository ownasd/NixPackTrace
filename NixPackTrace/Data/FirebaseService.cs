using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NixPackTrace.Core;
using NixPackTrace.Models;

namespace NixPackTrace.Data
{
    /// <summary>
    /// Communicates with Firebase Realtime Database via its REST API.
    /// 
    /// Database structure (shared with Assembly app):
    ///   Root: EndToEndTraceability
    ///   Assembly data path : EndToEndTraceability/{MAC_ID}/AssemblyApp.json
    ///   Packing  data path : EndToEndTraceability/{MAC_ID}/PackingApp.json
    /// </summary>
    public class FirebaseService
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        /// <summary>Base URL without trailing slash, e.g. https://xxx-default-rtdb.firebaseio.com</summary>
        private string BaseUrl => AppState.Settings.FirebaseUrl.TrimEnd('/');

        public bool IsOnline { get; private set; } = true;

        // ─── Validation ───────────────────────────────────────────────────────────

        /// <summary>
        /// Step 1 — Reads AssemblyApp.json for this MAC ID from Firebase and validates:
        ///   • Assembly record exists
        ///   • Assembly overall status = "OK"  (field name saved by NixTraceability: not nested, root level)
        ///   • Unit not already packed (PackingApp.json must be null)
        ///
        /// Returns (true, "") on success, or (false, "human readable reason") on failure.
        /// </summary>
        public async Task<(bool success, string error, AssemblyInfo? info)> ValidateMacAsync(string macId)
        {
            try
            {
                // ── 1. Read AssemblyApp.json ───────────────────────────────────────
                string assemblyUrl = $"{BaseUrl}/EndToEndTraceability/{SanitizeKey(macId)}/AssemblyApp.json";
                var assemblyResponse = await _http.GetAsync(assemblyUrl);

                if (!assemblyResponse.IsSuccessStatusCode)
                    return (false, $"Firebase HTTP error {(int)assemblyResponse.StatusCode}", null);

                string assemblyJson = await assemblyResponse.Content.ReadAsStringAsync();

                if (assemblyJson == "null" || string.IsNullOrWhiteSpace(assemblyJson))
                    return (false, "Assembly record NOT FOUND — unit not assembled yet", null);

                // ── 2. Parse assembly data ─────────────────────────────────────────
                using JsonDocument assemblyDoc = JsonDocument.Parse(assemblyJson);
                JsonElement root = assemblyDoc.RootElement;

                // The assembly app saves a flat JSON with these fields:
                // { RecordId, Operator, Shift, Batch, Timestamp, StationName, Parts: {...} }
                // No explicit "status" field at root — existence of the node means assembly was done as OK.
                // If the assembly app was updated to save status, check for it:
                string assemblyStatus = "OK"; // default — node presence = OK assembly
                if (root.TryGetProperty("Status", out JsonElement statusEl))
                    assemblyStatus = statusEl.GetString() ?? "OK";
                else if (root.TryGetProperty("status", out JsonElement statusElLower))
                    assemblyStatus = statusElLower.GetString() ?? "OK";

                if (!assemblyStatus.Equals("OK", StringComparison.OrdinalIgnoreCase))
                    return (false, $"Assembly status is '{assemblyStatus}' — not OK", null);

                // ── 3. Extract assembly info to display ────────────────────────────
                var info = new AssemblyInfo
                {
                    MacId       = macId,
                    StationName = root.TryGetProperty("StationName", out var sn) ? sn.GetString() ?? "" : "",
                    Operator    = root.TryGetProperty("Operator", out var op) ? op.GetString() ?? "" : "",
                    Shift       = root.TryGetProperty("Shift", out var sh) ? sh.GetString() ?? "" : "",
                    Batch       = root.TryGetProperty("Batch", out var ba) ? ba.GetString() ?? "" : "",
                    Timestamp   = root.TryGetProperty("Timestamp", out var ts) ? ts.GetString() ?? "" : "",
                };

                // Extract parts dict if present
                if (root.TryGetProperty("Parts", out JsonElement parts) && parts.ValueKind == JsonValueKind.Object)
                {
                    foreach (var part in parts.EnumerateObject())
                        info.Parts[part.Name] = part.Value.GetString() ?? "";
                }

                // ── 4. Check not already packed ────────────────────────────────────
                string packingUrl = $"{BaseUrl}/EndToEndTraceability/{SanitizeKey(macId)}/PackingApp.json";
                var packingResponse = await _http.GetAsync(packingUrl);
                if (packingResponse.IsSuccessStatusCode)
                {
                    string packingJson = await packingResponse.Content.ReadAsStringAsync();
                    if (packingJson != "null" && !string.IsNullOrWhiteSpace(packingJson))
                    {
                        // Already has packing data — extract box number for a friendly message
                        try
                        {
                            using JsonDocument packDoc = JsonDocument.Parse(packingJson);
                            string boxNo = packDoc.RootElement.TryGetProperty("BoxNo", out var bx)
                                ? bx.GetString() ?? "?"
                                : "?";
                            return (false, $"Already Packed in Box {boxNo}", null);
                        }
                        catch
                        {
                            return (false, "Already Packed", null);
                        }
                    }
                }

                IsOnline = true;
                return (true, "", info);
            }
            catch (Exception ex)
            {
                IsOnline = false;
                return (false, $"Offline / {ex.Message}", null);
            }
        }

        // ─── Update packing data ──────────────────────────────────────────────────

        /// <summary>
        /// Writes packing data to EndToEndTraceability/{MAC_ID}/PackingApp.json
        /// Mirrors the same naming conventions used by the Assembly app.
        /// </summary>
        public async Task<bool> UpdatePackingAsync(PackingRecord record)
        {
            try
            {
                var payload = new
                {
                    MacId      = record.MAC_ID,
                    BoxNo      = record.BOX_NO,
                    PackedAt   = record.TIMESTAMP.ToString("yyyy-MM-dd HH:mm:ss"),
                    PackedBy   = record.PACKED_BY,
                    LongQR     = record.LONG_QR,
                    ShortQR    = record.SHORT_QR,
                    TestingQR  = record.TESTING_QR,
                    Status     = record.STATUS,
                    StationName= AppState.Settings.StationName
                };

                string url  = $"{BaseUrl}/EndToEndTraceability/{SanitizeKey(record.MAC_ID)}/PackingApp.json";
                string body = JsonSerializer.Serialize(payload);

                var content  = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await _http.PutAsync(url, content);   // PUT = set/overwrite

                IsOnline = true;
                return response.IsSuccessStatusCode;
            }
            catch
            {
                IsOnline = false;
                return false;
            }
        }

        public async Task<bool> DeletePackingAsync(string macId)
        {
            try
            {
                string url  = $"{BaseUrl}/EndToEndTraceability/{SanitizeKey(macId)}/PackingApp.json";
                var response = await _http.DeleteAsync(url);
                IsOnline = true;
                return response.IsSuccessStatusCode;
            }
            catch
            {
                IsOnline = false;
                return false;
            }
        }

        // ─── Update dispatch data ──────────────────────────────────────────────────

        public async Task<bool> UpdateDispatchAsync(DispatchRecord record)
        {
            try
            {
                var payload = new
                {
                    DispatchId = record.DispatchId,
                    FromBoxNo = record.FromBoxNo,
                    ToBoxNo = record.ToBoxNo,
                    DispatchDate = record.DispatchDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    DispatchedBy = record.DispatchedBy,
                    Remarks = record.Remarks
                };

                string url = $"{BaseUrl}/Dispatches/{record.DispatchId}.json";
                string body = JsonSerializer.Serialize(payload);

                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await _http.PutAsync(url, content);

                IsOnline = true;
                return response.IsSuccessStatusCode;
            }
            catch
            {
                IsOnline = false;
                return false;
            }
        }

        public async Task<bool> DeleteDispatchAsync(string dispatchId)
        {
            try
            {
                string url = $"{BaseUrl}/Dispatches/{dispatchId}.json";
                var response = await _http.DeleteAsync(url);
                IsOnline = true;
                return response.IsSuccessStatusCode;
            }
            catch
            {
                IsOnline = false;
                return false;
            }
        }

        // ─── Helper ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Firebase RTDB keys cannot contain # $ [ ] /
        /// Colons (:) ARE valid Firebase key characters, so we do NOT replace them.
        /// The assembly app (NixTraceability) stores keys with raw colons, e.g. AA:BB:CC:DD:EE:FF.
        /// We must use the same raw key to find the data.
        /// Only replace truly illegal chars: # $ [ ] /
        /// </summary>
        private static string SanitizeKey(string key) =>
            key.Replace("#", "_").Replace("$", "_").Replace("[", "_").Replace("]", "_").Replace("/", "_");
    }

    /// <summary>Assembly data fetched from Firebase to display to the operator.</summary>
    public class AssemblyInfo
    {
        public string MacId       { get; set; } = "";
        public string StationName { get; set; } = "";
        public string Operator    { get; set; } = "";
        public string Shift       { get; set; } = "";
        public string Batch       { get; set; } = "";
        public string Timestamp   { get; set; } = "";
        public Dictionary<string, string> Parts { get; set; } = new();
    }
}

