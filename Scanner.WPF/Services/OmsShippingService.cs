using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Scanner.WPF.Services
{
    public sealed class OmsShipment
    {
        public string CustomerNo { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public decimal Length { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
        public decimal Weight { get; set; }

        public JObject Build(JObject shipper, string productId, JToken inchPoundUnit)
        {
            var parts = (Address ?? "").Split('/').Select(s => s.Trim()).ToArray();
            if (parts.Length != 5 || parts.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("地址格式须为：街道/城市/州/邮编/美国。");
            if (parts[4] != "美国" && !parts[4].Equals("US", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("当前流程适用于美国境内面单。");
            if (string.IsNullOrWhiteSpace(CustomerNo) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Phone) || string.IsNullOrWhiteSpace(productId)) throw new ArgumentException("请填写客户单号、姓名、电话并选择渠道。");
            if (Length <= 0 || Width <= 0 || Height <= 0 || Weight <= 0) throw new ArgumentException("长宽高及重量必须大于零。");
            var address = new JObject();
            foreach (var field in new[] { "Name", "CompanyName", "CountryCode", "StateOrProvince", "City", "Area", "Address1", "Address2", "Address3", "PostCode", "HouseNumber", "Email", "Phone", "Code" })
                address["shipper" + field] = shipper["shipper" + field]?.DeepClone() ?? JValue.CreateNull();
            if (string.IsNullOrWhiteSpace((string)address["shipperAddress1"]) || string.IsNullOrWhiteSpace((string)address["shipperPostCode"])) throw new InvalidDataException("发件地址不完整。");
            address["consigneeName"] = Name.Trim(); address["consigneeEmail"] = (Email ?? "").Trim(); address["consigneePhone"] = Phone.Trim();
            address["consigneeCountryCode"] = "US"; address["consigneeStateOrProvince"] = parts[2]; address["consigneeCity"] = parts[1];
            address["consigneeAddress1"] = System.Text.RegularExpressions.Regex.Replace(parts[0], @"\s+", " "); address["consigneePostCode"] = parts[3].Split('-')[0];
            return new JObject
            {
                ["customerNo"] = CustomerNo.Trim(), ["productId"] = productId, ["weightSizeUnit"] = inchPoundUnit.DeepClone(), ["currencyCode"] = "USD",
                ["goodsType"] = 3, ["boxCount"] = 1, ["multipleFlag"] = 1, ["signatureService"] = "", ["consigneeShipper"] = address,
                ["orderDetailList"] = new JArray(new JObject { ["boxNo"] = JValue.CreateNull(), ["boxQuantity"] = 1, ["boxWeight"] = Weight, ["boxLength"] = Length, ["boxWidth"] = Width, ["boxHeight"] = Height })
            };
        }
    }

    // Verified public website contracts. This client never logs credentials or retries payments.
    public sealed class OmsShippingService : IDisposable
    {
        public const string Origin = "https://ntm8zfn.oms.eccangtms.com";
        private readonly HttpClient _client;
        private readonly bool _owned;
        private string _token;
        public OmsShippingService(HttpClient client = null)
        {
            _owned = client == null;
            _client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = TimeSpan.FromSeconds(90) };
        }

        public async Task LoginAsync(string username, string password, CancellationToken token)
        {
            _token = null;
            var response = await SendAsync(HttpMethod.Post, "/alpaca-auth/login", new JObject { ["username"] = username.Trim(), ["password"] = password, ["remember_me"] = false, ["systemBelong"] = 3 }, token, true);
            if ((string)response["code"] != "1") throw new InvalidOperationException("OMS 登录失败，请检查账号或在网页完成验证码后重试。");
            _token = (string)response["result"]?["token"];
            if (string.IsNullOrWhiteSpace(_token)) throw new InvalidDataException("OMS 登录未返回会话。");
        }
        public async Task<JArray> ProductsAsync(CancellationToken token) => await GetAsync("/oms/order/getCommonProduct", token) as JArray ?? throw new InvalidDataException("渠道列表格式异常。");
        public async Task<JArray> ShippersAsync(CancellationToken token) => await GetAsync("/oms/omsUserShipperAddress/all", token) as JArray ?? throw new InvalidDataException("发件地址列表格式异常。");
        public async Task<JToken> InchPoundUnitAsync(CancellationToken token)
        {
            var units = await GetAsync("/sys/dict/getDictItems/weight_size_unit", token) as JArray ?? throw new InvalidDataException("单位列表格式异常。");
            var unit = units.OfType<JObject>().SingleOrDefault(u => ((string)u["text"] ?? "").Replace(" ", "").Contains("INCH/LB", StringComparison.OrdinalIgnoreCase));
            return unit?["value"]?.DeepClone() ?? throw new InvalidDataException("未找到英制(INCH/LB)单位。");
        }
        public async Task<JObject> QuoteAsync(JObject payload, CancellationToken token) => await PostAsync("/oms/order/submit", payload, token) as JObject ?? throw new InvalidDataException("试算结果格式异常。");
        public async Task<JObject> PayAsync(JObject payload, CancellationToken token) => await PostAsync("/oms/order/payOrder", payload, token) as JObject ?? throw new InvalidDataException("下单结果格式异常，请先核对订单列表，勿重复下单。");
        public async Task<JObject> PayOnceAsync(JObject payload, string ledgerDirectory, CancellationToken token)
        {
            string customer = (string)payload["customerNo"];
            if (string.IsNullOrWhiteSpace(customer)) throw new ArgumentException("客户单号不能为空。");
            Directory.CreateDirectory(ledgerDirectory);
            string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(customer.Trim())));
            string marker = Path.Combine(ledgerDirectory, key + ".json");
            var state = new JObject { ["customerNo"] = customer, ["status"] = "pending-or-unknown", ["createdAt"] = DateTimeOffset.Now.ToString("O") };
            // CreateNew makes a second click/restart fail before another billable request.
            using (var file = new FileStream(marker, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file)) writer.Write(state.ToString());
            var result = await PayAsync(payload, token); // Deliberately no automatic retry.
            state["status"] = "succeeded"; state["orderId"] = result["id"]?.DeepClone();
            File.WriteAllText(marker, state.ToString());
            return result;
        }
        public async Task<Uri> GetLabelAsync(string orderId, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(orderId)) throw new ArgumentException("请输入 OMS 订单 ID。");
            var data = await GetAsync("/oms/order/getLabel?ids=" + Uri.EscapeDataString(orderId.Trim()), token);
            var uri = new Uri((string)data?["mergeUrl"] ?? throw new InvalidDataException("订单暂未返回面单地址。"), UriKind.Absolute);
            ValidateLabelUri(uri); return uri;
        }
        public static void ValidateLabelUri(Uri uri)
        {
            if (uri.Scheme != "https" || uri.Host != "erp-us-oss.eccang.com" || !uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("面单地址不是已确认的 PDF 来源。");
        }
        public async Task DownloadLabelAsync(Uri uri, string path, CancellationToken token)
        {
            ValidateLabelUri(uri);
            // New request has no OMS authorization header: never forward credentials to storage.
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(token);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920]; int read;
            while ((read = await stream.ReadAsync(chunk, token)) > 0)
            {
                if (buffer.Length + read > 20 * 1024 * 1024) throw new InvalidDataException("面单 PDF 过大。");
                buffer.Write(chunk, 0, read);
            }
            var bytes = buffer.ToArray();
            if (bytes.Length < 5 || Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-") throw new InvalidDataException("下载内容不是 PDF。");
            await File.WriteAllBytesAsync(path, bytes, token);
        }
        private Task<JToken> GetAsync(string path, CancellationToken token) => SendAsync(HttpMethod.Get, path, null, token);
        private Task<JToken> PostAsync(string path, JObject data, CancellationToken token) => SendAsync(HttpMethod.Post, path, data, token);
        private async Task<JToken> SendAsync(HttpMethod method, string path, JObject data, CancellationToken token, bool login = false)
        {
            if (!login && string.IsNullOrWhiteSpace(_token)) throw new InvalidOperationException("请先登录 OMS。");
            string separator = path.Contains('?') ? "&" : "?";
            using var request = new HttpRequestMessage(method, Origin + "/alpaca-oms" + path + separator + "lang=");
            request.Headers.Add("X-System-Belong", "3");
            request.Headers.Add("Origin", Origin);
            if (!login) request.Headers.Add("X-Access-Token", _token);
            if (data != null) request.Content = new StringContent(data.ToString(Formatting.None), Encoding.UTF8, "application/json");
            using var response = await _client.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            var envelope = JObject.Parse(await response.Content.ReadAsStringAsync(token));
            if (login) return envelope;
            if ((bool?)envelope["success"] != true) throw new InvalidOperationException("OMS 操作未成功，代码：" + (string)envelope["code"] + "。请检查登录和订单状态。");
            return envelope["result"];
        }
        public void Dispose() { _token = null; if (_owned) _client.Dispose(); }
    }
}
