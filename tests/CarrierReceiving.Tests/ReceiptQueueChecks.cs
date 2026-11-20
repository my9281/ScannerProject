using Scanner.ShippingCowAndroid.Services;
using Scanner.Server.Model;
using Scanner.Web.Models;
using Scanner.Web.Services;
using Scanner.Server.DAL;
using MySqlConnector;
using System.Text.Json.Nodes;

static class ReceiptQueueChecks
{
    public static async Task<int> Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        var account = new AccountResult("receiver", null, new AccountDomain(7, "warehouse"), "user", "token", DateTimeOffset.UtcNow.AddHours(1)) { UserId = 42 };
        var context = new CarrierReceiptContext(7, 42, "receiver", "site-1");
        var noForecastDatabase = new NoReceiptDatabase();
        var independentContext = await new CarrierReceiptService(noForecastDatabase).Context(account, default);
        Check(independentContext.SiteId == "domain-7" && independentContext.UserId == 42 && noForecastDatabase.Calls == 0,
            "P0 context does not require forecast configuration or a database lookup");
        string folder = Path.Combine(Path.GetTempPath(), "receipt-outbox-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder); string path = Path.Combine(folder, "queue.json");
        try
        {
            const string composite = "[)>010208901840019384517236257FDEG7965416103131/111.33KGN520 Jersey AveNew BrunswickNJBLUETTI SPF RETURN0610ZGD01011ZBLUETTI POWER INC12Z516610832520Z31Z963200196079654161000038451723625732Z0234Z01";
            Check(CarrierReceiptValidation.NormalizeNumber(composite) == "960796541610000384517236257", "customer composite barcode extracts full tracking number");
            Check(CarrierReceiptValidation.NormalizeNumber("9632001960796541610000384517236257") == "960796541610000384517236257", "direct FedEx routing barcode extracts full tracking number");
            Check(CarrierReceiptValidation.ScanWarning("1Z1234567890123456", out var upsCarrier, out _) is not null && upsCarrier is null, "UPS 1Z is not reported as USPS");
            foreach (var (barcode, expectedCarrier, expectedNumber) in new[]
            {
                ("9405530109355007559621", "usps", "9405530109355007559621"),
                ("EC000000000US", "usps", "EC000000000US"),
                ("123456789012", "fedex", "123456789012"),
                ("9632001960796541610000384517236257", "fedex", "960796541610000384517236257"),
                ("tba123456789012", "tba", "TBA123456789012")
            })
                Check(CarrierReceiptValidation.ScanWarning(barcode, out var carrier, out var number) is null
                    && carrier == expectedCarrier && number == expectedNumber, "automatic carrier and number: " + barcode);
            var offlinePath = Path.Combine(folder, "offline.json");
            var offline = new CarrierReceivingQueue(offlinePath);
            offline.PrepareCapture("https://wms.example/", "receiver");
            offline.Add(composite, DateTimeOffset.UtcNow);
            var draft = offline.Pending.Single();
            Check(draft.DomainId == 0 && draft.RawBarcode == composite && offline.Rows.Count == 1, "first launch records before any successful connection");
            offline = new CarrierReceivingQueue(offlinePath);
            try { offline.Configure(context with { Server = "https://wrong.example/" }); throw new Exception("Draft server switched"); } catch (InvalidOperationException) { checks++; }
            var offlineContext = context with { Server = "https://wms.example/" };
            offline.Configure(offlineContext);
            Check(offline.Pending.Single().ScanId == draft.ScanId && offline.Pending.Single().ScannedAt == draft.ScannedAt && offline.Pending.Single().OperatorUserId == 42,
                "binding after restart preserves original scan identity and time");
            await offline.Upload(offlineContext, (batch, _) =>
            {
                try { offline.Delete(batch.Items[0].ScanId); throw new Exception("In-flight scan deleted"); } catch (InvalidOperationException) { checks++; }
                try { offline.ClearHistory(); throw new Exception("In-flight queue cleared"); } catch (InvalidOperationException) { checks++; }
                offline.Add("123456789013", DateTimeOffset.UtcNow);
                return Task.FromResult(new CarrierReceiptAcknowledgement(batch.Items.Select(s => s.ScanId).ToArray(), 1));
            }, default);
            Check(offline.Pending.Single().TrackingNumber == "123456789013" && offline.Rows.Count == 2, "scanning continues during upload and new scan remains pending");
            Check(offline.Add("_v", DateTimeOffset.UtcNow) is not null && offline.Rows.Last().Scan.RawBarcode == "_v" && offline.Rows.Last().Warning is not null
                && offline.Pending.Count == 1, "region label is retained in table with warning and never uploaded as parcel");
            Check(offline.Add("1Z1234567890123456", DateTimeOffset.UtcNow) is not null, "unsupported UPS barcode warns without queuing");
            offline.Add("123456789013", DateTimeOffset.UtcNow);
            Check(offline.Pending.Count == 2 && offline.Rows.Count == 5, "repeat scans are retained as separate events without interception");
            Check(new CarrierReceivingQueue(offlinePath).Rows.Last(r => r.Warning is not null).Scan.RawBarcode == "1Z1234567890123456",
                "warning rows survive restart without blocking valid pending parcels");
            int priorPending = offline.Pending.Count;
            Check(offline.Add("420089014134", DateTimeOffset.UtcNow) is not null && offline.Pending.Count == priorPending,
                "numeric region barcode alarms and does not upload");
            Check(offline.Rows.Last().Scan.RawBarcode == "420089014134" && offline.Rows.Last().Warning is not null, "numeric region barcode retained as warning row");
            Check(offline.Add("420089019234690213424550799901", DateTimeOffset.UtcNow) is null,
                "site FedEx routing barcode is automatically recognized");
            var routing = offline.Pending.Last();
            Check(routing.Carrier == "fedex" && routing.TrackingNumber == routing.RawBarcode && routing.TrackingNumber == "420089019234690213424550799901",
                "FedEx complete 420 code is uploaded without stripping ZIP prefix");
            Check(offline.Add("TBA123456789012", DateTimeOffset.UtcNow) is null && offline.Pending.Last().Carrier == "tba", "TBA scan automatically classified separately");
            CarrierReceiptService.ValidateBatch(new([offline.Pending.Last()]), account); checks++;
            Check(new CarrierReceivingQueue(offlinePath).Pending.Last().Carrier == "tba", "TBA persists across restart");
            const string separatedRouting = "42008901\u001d9234690213424550799901";
            Check(offline.Add(separatedRouting, DateTimeOffset.UtcNow) is null, "group separator in photographed FedEx barcode is accepted");
            var separated = offline.Pending.Last();
            Check(separated.Carrier == "fedex" && separated.TrackingNumber == "420089019234690213424550799901" && separated.RawBarcode == separatedRouting,
                "group separator is excluded from number while full raw evidence is preserved");
            CarrierReceiptService.ValidateBatch(new([separated]), account); checks++;
            Check(new CarrierReceivingQueue(offlinePath).Pending.Last() == separated, "separated barcode survives restart and upload validation");
            Check(CarrierReceiptValidation.ScanWarning("42008901\u001d4134", out _, out _) is not null, "region code still alarms with separator");
            Check(CarrierReceiptValidation.NormalizeNumber("\u001e" + composite + "\u0004") == "960796541610000384517236257", "composite scanner framing does not affect extraction");
            File.Delete(offlinePath);
            var queue = new CarrierReceivingQueue(path);
            try { queue.Add("123456789012", DateTimeOffset.UtcNow); throw new Exception("Anonymous scan accepted"); }
            catch (InvalidOperationException) { checks++; }
            queue.Configure(context); queue.Add("123456789012\r\n", DateTimeOffset.UtcNow);
            var original = queue.Pending.Single();
            var pdaAccount = account with { Username = "PDA01" };
            var attributed = CarrierReceiptService.AttributePdaReceipt(new([original with { DomainId = 0, OperatorUserId = 0 }]), pdaAccount);
            CarrierReceiptService.ValidateBatch(attributed, pdaAccount);
            Check(attributed.Items.Single() == original, "PDA attribution uses authenticated server identity and preserves scan evidence");
            var otherDomain = original with { DomainId = 999 };
            Check(CarrierReceiptService.AttributePdaReceipt(new([otherDomain]), pdaAccount).Items.Single() == otherDomain,
                "PDA receiver cannot reattribute records captured in another domain");
            Check(CarrierReceiptService.AttributePdaReceipt(new([original]), account).Items.Single() == original, "ordinary receiver attribution is unchanged");
            Check(queue.Rows.Single() is { Uploaded: false } && queue.Rows.Single().Scan.ScanId == original.ScanId,
                "new receipt immediately appears in durable table without a forecast");
            Check(original.TrackingNumber == "123456789012" && original.RawBarcode.EndsWith("\r\n"), "normalize scanner suffix but retain evidence");
            Check(original.DomainId == 7 && original.OperatorUserId == 42 && original.SiteId == "site-1", "capture immutable scope");
            CarrierReceiptService.ValidateBatch(new(queue.Pending), account); checks++;
            try { CarrierReceiptService.ValidateBatch(new(queue.Pending), account with { UserId = 43 }); throw new Exception("Cross-user upload accepted"); }
            catch (ApiException ex) { Check(ex.StatusCode == 403, "deny spoofed operator"); }
            try { CarrierReceiptService.ValidateBatch(new([original, original]), account); throw new Exception("Duplicate scan IDs accepted"); }
            catch (ArgumentException) { checks++; }
            try { queue.NewBatch(); throw new Exception("Unconfirmed outbox discarded"); } catch (InvalidOperationException) { checks++; }
            try { queue.Configure(context with { UserId = 43 }); throw new Exception("Queue reassigned"); } catch (InvalidOperationException) { checks++; }
            try { queue.Configure(context with { Server = "https://other.example/" }); throw new Exception("Queue moved to another server"); } catch (InvalidOperationException) { checks++; }
            queue.Configure(context with { SiteId = "site-2" });
            Check(queue.Context!.SiteId == "site-1" && queue.Pending.Single().SiteId == "site-1", "credential refresh cannot reassign offline site");
            var legacy = JsonNode.Parse(File.ReadAllText(path))!.AsObject(); legacy.Remove("Rows");
            File.WriteAllText(path, legacy.ToJsonString());
            var restored = new CarrierReceivingQueue(path);
            Check(restored.Pending.Single() == original && restored.DeviceId == queue.DeviceId, "durable restart preserves IDs and contents");
            Check(restored.Rows.Single().Scan == original && !restored.Rows.Single().Uploaded, "old queue upgrades into table rows without changing scan contents");
            try { await restored.Upload(context, (_, _) => throw new HttpRequestException("disconnected"), default); throw new Exception("Expected network failure"); }
            catch (HttpRequestException) { Check(restored.Pending.Single().ScanId == original.ScanId, "ambiguous response retains original ID"); }
            try { await restored.Upload(context, (_, _) => Task.FromResult(new CarrierReceiptAcknowledgement([], 0)), default); throw new Exception("Partial acknowledgement accepted"); }
            catch (InvalidOperationException) { Check(restored.Pending.Count == 1, "incomplete ACK retains queue"); }
            int accepted = await restored.Upload(context, (batch, _) => Task.FromResult(new CarrierReceiptAcknowledgement(batch.Items.Select(x => x.ScanId).ToArray(), 1)), default);
            Check(accepted == 1 && restored.Pending.Count == 0 && new CarrierReceivingQueue(path).Pending.Count == 0, "only exact ACK removes durable records");
            Check(restored.Rows.Single() is { Uploaded: true } && new CarrierReceivingQueue(path).Rows.Single().Scan == original,
                "successful submit keeps visible table record and persists uploaded status");
            Check(restored.BatchId == original.BatchId, "submit does not rotate batch");
            restored.NewBatch(); restored.Add("9400111899560000000000", DateTimeOffset.UtcNow);
            Check(restored.BatchId != original.BatchId, "explicit new batch identity");
            Directory.CreateDirectory(path + ".tmp");
            try { await restored.Upload(context, (batch, _) => Task.FromResult(new CarrierReceiptAcknowledgement(batch.Items.Select(x => x.ScanId).ToArray(), 1)), default); throw new Exception("Expected durable ACK save failure"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { Check(restored.Pending.Count == 1 && new CarrierReceivingQueue(path).Pending.Count == 1, "disk failure after ACK retains memory and disk queue"); }
            finally { Directory.Delete(path + ".tmp"); }
            try { CarrierReceiptValidation.Validate(original with { TrackingNumber = "999999999999" }, DateTimeOffset.UtcNow); throw new Exception("Mismatch accepted"); }
            catch (ArgumentException) { checks++; }
            try { CarrierReceiptValidation.Validate(original with { ScannedAt = DateTimeOffset.UtcNow.AddDays(1) }, DateTimeOffset.UtcNow); throw new Exception("Future time accepted"); }
            catch (ArgumentException) { checks++; }
            var incompatible = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            incompatible["Pending"]![0]!["TrackingNumber"] = "OLD-MISMATCH";
            File.WriteAllText(path, incompatible.ToJsonString());
            var cleared = new CarrierReceivingQueue(path);
            Check(cleared.Pending.Count == 0 && cleared.Rows.Count == 0 && new CarrierReceivingQueue(path).Pending.Count == 0,
                "incompatible historical scans are cleared durably instead of blocking receipt page");
            cleared.Add("123456789014", DateTimeOffset.UtcNow);
            Check(cleared.Pending.Count == 1, "scanning resumes after incompatible history is discarded");
            cleared.Add("_v", DateTimeOffset.UtcNow);
            var warningId = cleared.Rows.Last().Scan.ScanId;
            cleared.Delete(warningId);
            Check(cleared.Rows.Count == 1 && cleared.Pending.Count == 1, "single warning deletion preserves valid pending scans");
            cleared.Delete(cleared.Pending.Single().ScanId);
            Check(new CarrierReceivingQueue(path).Pending.Count == 0 && new CarrierReceivingQueue(path).Rows.Count == 0, "single pending deletion persists");
            cleared.Add("123456789015", DateTimeOffset.UtcNow);
            var beforeClearBatch = cleared.BatchId;
            cleared.ClearHistory();
            Check(cleared.Rows.Count == 0 && cleared.Pending.Count == 0 && cleared.BatchId != beforeClearBatch && new CarrierReceivingQueue(path).Rows.Count == 0,
                "explicit clear cache resets records durably and starts fresh batch");
            File.WriteAllText(path, "corrupt-cache");
            try { new CarrierReceivingQueue(path); throw new Exception("Corrupt cache silently reset"); }
            catch (System.Text.Json.JsonException) { Check(File.ReadAllText(path) == "corrupt-cache", "corrupted cache preserved"); }
        }
        finally { foreach (string name in new[] { path, path + ".tmp" }) if (File.Exists(name)) File.Delete(name); Directory.Delete(folder); }
        return checks;
    }
    private sealed class NoReceiptDatabase : IMySqlConnectionFactory
    {
        public int Calls;
        public Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        { Calls++; throw new Exception("Receipt context must not query customer forecast configuration"); }
    }
}
