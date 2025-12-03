using ApiMMC.Models.Entities;
using ApiMMC.Services.Helpers.Settings;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ApiMMC.Services.Helpers.Integration
{
    public class RequestHttp(
        AppSettings appSettings,
        IHttpClientFactory httpClientFactory)
    {
        private readonly IntegrationSettings _settings = appSettings.Integration;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public enum TypeBody
        {
            Body,                   // JSON en el cuerpo
            Query,                  // Parámetros en la URL
            FormUrlEncoded,         // Parámetros en el cuerpo (x-www-form-urlencoded)
            QueryAndFormUrlEncoded, // Algunos en la URL, otros en el body
            QueryAndBody,
            Multipart
        }

        public class MultipartBody
        {
            public Dictionary<string, string> Fields { get; set; } = [];
            public List<FileMultipart> Files { get; set; } = [];
        }

        public class FileMultipart
        {
            public string FieldName { get; set; }     // e.g. "ArchivoZip"
            public string FileName { get; set; }      // e.g. "reportelecturas.zip"
            public string ContentType { get; set; }   // e.g. "application/zip"
            public byte[] Bytes { get; set; }         // archivo en bytes
        }

        // Cache del token
        private static string _cachedToken;
        private static DateTime _tokenExpiration = DateTime.MinValue;
        private readonly SemaphoreSlim _tokenLock = new(1, 1);

        public record HybridBody(Dictionary<string, string> Query, Dictionary<string, string> Form, object Body);

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<TR> CallMethod<TR>(
            string service,
            string action,
            object model,
            HttpMethod method,
            TypeBody? typeBody = null,
            string token = null,
            CancellationToken ct = default)
        {
            // ================== RESOLVER SERVICIO Y ACCIÓN ==================
            var _service = _settings.Services.FirstOrDefault(s => s.Name.Equals(service))
                ?? throw new InvalidOperationException($"Servicio '{service}' no encontrado.");

            var url = _service.Methods.FirstOrDefault(a => a.Method.Equals(action)).Value
                ?? throw new InvalidOperationException($"Acción '{action}' no encontrada para '{service}'.");

            // ================== CONSTRUIR CUERPO ==================
            var content = BuildContent(typeBody, model, ref url);

            // ================== CLIENTE ==================
            var client = _httpClientFactory.CreateClient("DefaultClient");

            using var request = new HttpRequestMessage(method, url)
            {
                Version = HttpVersion.Version11
            };

            if (string.IsNullOrEmpty(token) && service.Equals("xmLecturas", StringComparison.OrdinalIgnoreCase))
                token = await GetAccessTokenAsync(ct);

            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            if (method != HttpMethod.Get && typeBody != TypeBody.Query)
                request.Content = content;

            // ================== EXECUTE ==================
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            var text = await response.Content.ReadAsStringAsync(ct);

            // ================== ERROR ==================
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Error {response.StatusCode}: {text}");

            return string.IsNullOrEmpty(text) ? default : JsonSerializer.Deserialize<TR>(text, JsonOptions);
        }

        private async Task<string> GetAccessTokenAsync(CancellationToken ct)
        {
            await _tokenLock.WaitAsync(ct);
            try
            {
                if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _tokenExpiration)
                    return _cachedToken;
                var service = _settings.Services.FirstOrDefault(s => s.Name.Equals("xmToken", StringComparison.OrdinalIgnoreCase));

                var url = service.Methods.FirstOrDefault(m => m.Method.Equals("token", StringComparison.OrdinalIgnoreCase))?.Value;

                var client = _httpClientFactory.CreateClient("DefaultClient");

                var data = new Dictionary<string, string>
                {
                    ["client_id"] = service.Authentication.User,
                    ["client_secret"] = service.Authentication.Pass,
                    ["scope"] = "https://b2cbibcomptranssgprb.onmicrosoft.com/f5b2f167-677f-4040-b60b-dd76557aa379/.default",
                    ["grant_type"] = "client_credentials"
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new FormUrlEncodedContent(data)
                };

                using var response = await client.SendAsync(request, ct);

                var json = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Error obteniendo token XM Lecturas: {json}");

                var tokenResponse = JsonSerializer.Deserialize<XmToken>(json, JsonOptions);

                _cachedToken = tokenResponse?.AccessToken ?? throw new InvalidOperationException("Token vacío en respuesta XM");
                _tokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresInSeconds - 60); // margen de seguridad

                return _cachedToken;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private static void AddQueryParams(ref string url, Dictionary<string, string> query)
        {
            if (query == null || query.Count == 0)
                return;

            var q = string.Join("&", query.Select(i => $"{i.Key}={Uri.EscapeDataString(i.Value)}"));
            url += url.Contains('?') ? "&" + q : "?" + q;
        }

        private static HttpContent BuildContent(TypeBody? typeBody, object model, ref string url)
        {
            if (model == null || typeBody == null)
                return null;

            return typeBody switch
            {
                TypeBody.Body =>
                    AddBody(model),

                TypeBody.Query =>
                    AddQueryThenReturnNull((Dictionary<string, string>)model, ref url),

                TypeBody.FormUrlEncoded =>
                    new FormUrlEncodedContent((Dictionary<string, string>)model),

                TypeBody.QueryAndFormUrlEncoded =>
                    BuildHybridForm((HybridBody)model, ref url),

                TypeBody.QueryAndBody =>
                    BuildHybridJson((HybridBody)model, ref url),

                TypeBody.Multipart =>
                    BuildMultipart((MultipartBody)model),

                _ => null
            };
        }

        private static StringContent AddBody(object model)
        {
            var json = model is string s ? s : JsonSerializer.Serialize(model, JsonOptions);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private static HttpContent AddQueryThenReturnNull(Dictionary<string, string> query, ref string url)
        {
            AddQueryParams(ref url, query);
            return null;
        }

        private static FormUrlEncodedContent BuildHybridForm(HybridBody hybrid, ref string url)
        {
            AddQueryParams(ref url, hybrid.Query);
            return hybrid.Form.Count == 0 ? null : new FormUrlEncodedContent(hybrid.Form);
        }

        private static StringContent BuildHybridJson(HybridBody hybrid, ref string url)
        {
            AddQueryParams(ref url, hybrid.Query);

            return hybrid.Body == null
                ? null
                : new StringContent(JsonSerializer.Serialize(hybrid.Body), Encoding.UTF8, "application/json");
        }

        private static MultipartFormDataContent BuildMultipart(MultipartBody model)
        {
            var multipart = new MultipartFormDataContent();

            // Campos normales
            if (model.Fields != null)
            {
                foreach (var kv in model.Fields)
                    multipart.Add(new StringContent(kv.Value), kv.Key);
            }

            // Archivos
            if (model.Files != null)
            {
                foreach (var file in model.Files)
                {
                    var fileContent = new ByteArrayContent(file.Bytes);
                    if (!string.IsNullOrWhiteSpace(file.ContentType))
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);

                    multipart.Add(fileContent, file.FieldName, file.FileName);
                }
            }

            return multipart;
        }
    }
}
