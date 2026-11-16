using Scanner.AndroidTester.Services;
using Scanner.Server.Model;
using Scanner.Web.Models;
using Scanner.Web.Services;

static class ReceiptQueueChecks
{
    public static async Task<int> Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        var account = new AccountResult("receiver", null, new AccountDomain(7, "warehouse"), "user", "token", DateTimeOffset.UtcNow.AddHours(1)) { UserId = 42 };
        var context = new CarrierReceiptContext(7, 42, "receiver", "site-1");
        string folder = Path.Combine(Path.GetTempPath(), "receipt-outbox-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder); string path = Path.Combine(folder, "queue.json");
        try
        {
            var queue = new CarrierReceivingQueue(path);
            try { queue.Add("fedex", "123456789012", DateTimeOffset.UtcNow); throw new Exception("Anonymous scan accepted"); }
            catch (InvalidOperationException) { checks++; }
            queue.Configure(context); queue.Add("fedex", "123456789012\r\n", DateTimeOffset.UtcNow);
            var original = queue.Pending.Single();
            Check(original.TrackingNumber == "123456789012" && original.RawBarcode.EndsWith("\r\n"), "normalize scanner suffix but retain evidence");
            Check(original.DomainId == 7 && original.OperatorUserId == 42 && original.SiteId == "site-1", "capture immutable scope");
            CarrierReceiptService.ValidateBatch(new(queue.Pending), account); checks++;
            try { CarrierReceiptService.ValidateBatch(new(queue.Pending), account with { UserId = 43 }); throw new Exception("Cross-user upload accepted"); }
            catch (ApiException ex) { Check(ex.StatusCode == 403, "deny spoofed operator"); }
            try { CarrierReceiptService.ValidateBatch(new([original, original]), account); throw new Exception("Duplicate scan IDs accepted"); }
            catch (ArgumentException) { checks++; }
            try { queue.Add("fedex", "123456789012", DateTimeOffset.UtcNow); throw new Exception("Duplicate package accepted"); }
            catch (ArgumentException) { checks++; }
            try { queue.NewBatch(); throw new Exception("Unconfirmed outbox discarded"); } catch (InvalidOperationException) { checks++; }
            try { queue.Configure(context with { UserId = 43 }); throw new Exception("Queue reassigned"); } catch (InvalidOperationException) { checks++; }
            try { queue.Configure(context with { Server = "https://other.example/" }); throw new Exception("Queue moved to another server"); } catch (InvalidOperationException) { checks++; }
            queue.Configure(context with { SiteId = "site-2" });
            Check(queue.Context!.SiteId == "site-1" && queue.Pending.Single().SiteId == "site-1", "credential refresh cannot reassign offline site");
            var restored = new CarrierReceivingQueue(path);
            Check(restored.Pending.Single() == original && restored.DeviceId == queue.DeviceId, "durable restart preserves IDs and contents");
            try { await restored.Upload(context, (_, _) => throw new HttpRequestException("disconnected"), default); throw new Exception("Expected network failure"); }
            catch (HttpRequestException) { Check(restored.Pending.Single().ScanId == original.ScanId, "ambiguous response retains original ID"); }
            try { await restored.Upload(context, (_, _) => Task.FromResult(new CarrierReceiptAcknowledgement([], 0)), default); throw new Exception("Partial acknowledgement accepted"); }
            catch (InvalidOperationException) { Check(restored.Pending.Count == 1, "incomplete ACK retains queue"); }
            int accepted = await restored.Upload(context, (batch, _) => Task.FromResult(new CarrierReceiptAcknowledgement(batch.Items.Select(x => x.ScanId).ToArray(), 1)), default);
            Check(accepted == 1 && restored.Pending.Count == 0 && new CarrierReceivingQueue(path).Pending.Count == 0, "only exact ACK removes durable records");
            try { restored.Add("fedex", "123456789012", DateTimeOffset.UtcNow); throw new Exception("Same-batch repeat after ACK accepted"); }
            catch (ArgumentException) { checks++; }
            restored.NewBatch(); restored.Add("usps", "9400111899560000000000", DateTimeOffset.UtcNow);
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
            File.WriteAllText(path, "corrupt-cache");
            try { new CarrierReceivingQueue(path); throw new Exception("Corrupt cache silently reset"); }
            catch (System.Text.Json.JsonException) { Check(File.ReadAllText(path) == "corrupt-cache", "corrupted cache preserved"); }
        }
        finally { foreach (string name in new[] { path, path + ".tmp" }) if (File.Exists(name)) File.Delete(name); Directory.Delete(folder); }
        return checks;
    }
}
