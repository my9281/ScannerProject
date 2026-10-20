using Microsoft.Extensions.Options;
using Scanner.Web.Services;
using System.Text.Json.Nodes;

static class BreezarySyncChecks
{
    public static async Task<int> RunAsync()
    {
        var checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        var options = Options.Create(new TencentDocsOptions());
        var target = BreezaryMerge.Target(options.Value);
        var store = new MemoryBreezaryStore();
        var remote = new Remote();
        var sync = new BreezarySyncService(store, remote, options);
        async Task<BreezaryRow> Row() => (await store.ReadAllAsync(target, default)).Single();
        async Task Edit(string state, bool rebase = false)
        {
            var row = await Row();
            await sync.EditAsync("r1", new(row.Version, new() { ["状态"] = state }, rebase), default);
        }
        Check((await sync.PullAsync(default)).Pulled == 1, "Pull into local store");
        await sync.PullAsync(default);
        Check((await store.ReadAllAsync(target, default)).Count == 1, "Repeated pull never duplicates record");
        var calls = remote.Reads;
        var page = await sync.ListAsync(0, 100, default);
        Check(page.Total == 1 && remote.Reads == calls && page.Source == "database", "Daily reads only access local database");
        var stale = (await Row()).Version;
        await Edit("已完成");
        Check((await Row()).Pending.Count == 1 && (await Row()).Values["状态"]!.ToString() == "已完成", "Edit writes pending local delta");
        Check(remote.Reads == calls && remote.Writes == 0, "Local edits do not call Tencent");
        try { await sync.EditAsync("r1", new(stale, new() { ["状态"] = "过时改动" }), default); throw new Exception("Stale edit accepted"); }
        catch (ApiException ex) { Check(ex.StatusCode == 409, "Optimistic concurrency rejects stale edit"); }
        remote.Values["备注"] = "腾讯新增备注";
        await sync.PullAsync(default);
        Check((await Row()).Values["状态"]!.ToString() == "已完成" && (await Row()).Values["备注"]!.ToString() == "腾讯新增备注", "Pull merges unrelated fields without losing local edit");
        var push = await sync.PushAsync(default);
        Check(push.Pushed == 1 && remote.Values["状态"]!.ToString() == "已完成" && remote.Values["备注"]!.ToString() == "腾讯新增备注", "Push updates only changed fields");
        Check(remote.LastPatch?.Count == 1 && (await Row()).Pending.Count == 0 && (await Row()).SyncStatus == "clean", "Acknowledged push clears queue");
        calls = remote.Reads;
        await sync.PushAsync(default);
        Check(remote.Reads == calls, "No pending changes means no remote request");
        await Edit("已签收");
        remote.Values["状态"] = "人工处理";
        var writes = remote.Writes;
        push = await sync.PushAsync(default);
        Check(push.Conflicts == 1 && remote.Writes == writes && (await Row()).Pending.Count == 1, "Same field remote edit is not overwritten");
        Check((await Row()).Remote["状态"]!.ToString() == "人工处理", "Conflict exposes latest Tencent value for review");
        await Edit("已签收", rebase: true);
        Check((await sync.PushAsync(default)).Pushed == 1, "Explicit rebase resolves reviewed conflict");
        await Edit("失败后重试");
        remote.FailWrite = true;
        push = await sync.PushAsync(default);
        Check(push.Failed == 1 && (await Row()).Pending.Count == 1 && (await Row()).SyncStatus == "error", "Failed push preserves changes for retry");
        remote.FailWrite = false;
        Check((await sync.PushAsync(default)).Pushed == 1, "Failed push can be retried");
        await Edit("远端成功但本地保存失败");
        store.FailNextSave = true;
        push = await sync.PushAsync(default);
        writes = remote.Writes;
        Check(push.Failed == 1 && (await Row()).Pending.Count == 1, "Database failure after remote update does not lose pending change");
        push = await sync.PushAsync(default);
        Check(push.Pushed == 1 && remote.Writes == writes, "Retry recognizes already-applied remote values without duplicate push");
        await Edit("取消改动");
        await Edit((await Row()).Remote["状态"]!.ToString());
        Check((await Row()).Pending.Count == 0, "Restoring remote value clears pending delta");
        await Edit("远端已删除");
        remote.Deleted = true;
        Check((await sync.PushAsync(default)).Conflicts == 1 && (await Row()).Pending.Count == 1, "Missing remote record is a conflict, not implicit recreation");
        await sync.PullAsync(default);
        Check((await sync.ListAsync(0, 100, default)).Total == 1, "Remote deletions do not delete local history");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        try { await sync.PullAsync(cancel.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { checks++; }
        await using (var held = await store.AcquireAsync(target, default))
        {
            try { await sync.PushAsync(default); throw new Exception("Concurrent sync accepted"); }
            catch (ApiException ex) { Check(ex.StatusCode == 409, "Overlapping sync is rejected"); }
        }
        Check(BreezaryMerge.Target(new() { SheetId = "different" }) != target, "Different targets cannot mix data");
        var actual = JsonNode.Parse("""
            {"SKU":[{"format":{},"text":"35015-WG-XL","type":"text"}],"发货日期":"1792080000000",
             "备注":[{"format":{},"text":"样机","type":"text"}],"实际签收快递单号":[{"format":{},"text":"ACTUAL-001","type":"text"}],
             "工单号":[{"format":{},"text":"WO-001","type":"text"}],"是否签收":[{"id":"o6t6VV","style":7,"text":"是"}],
             "渠道":[{"id":"ood0SU","style":15,"text":"UPS"}],"状态流转":[{"id":"oj8yEV","style":15,"text":"样机留存"}],
             "签收日期":"1790697600000","运单号":[{"format":{},"text":"ORIGINAL-001","type":"text"}],
             "问题描述":[{"format":{},"text":"测试样机","type":"text"}]}
            """)!.AsObject();
        var sampleRow = new BreezaryRow { RecordId = "sample", Remote = actual, Version = 1 };
        var noteEdit = BreezaryMerge.Edit(sampleRow, new(1, new() { ["备注"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = "收货完成" }) }));
        Check(noteEdit.Pending.Count == 1 && noteEdit.Values.Count == 11 && noteEdit.Values["状态流转"]![0]!["id"]!.ToString() == "oj8yEV", "Actual receipt shape retains all fields and option identity");
        Check(BreezaryMerge.Text(actual["实际签收快递单号"]) != BreezaryMerge.Text(actual["运单号"]), "Original and actual tracking numbers are distinct");
        Check(noteEdit.Values["签收日期"]!.ToString() == "1790697600000", "Date milliseconds preserved for round trip");
        Check(BreezaryMerge.Equivalent(actual["SKU"], JsonNode.Parse("""[{"text":"35015-WG-XL","type":"text"}]""")), "Empty text format does not cause false conflicts");
        Check(BreezaryMerge.Equivalent(actual["是否签收"], JsonNode.Parse("""[{"id":"o6t6VV","style":8,"text":"是"}]""")), "Option display color does not cause false conflicts");
        try
        {
            BreezaryMerge.Edit(sampleRow, new(1, new() { ["状态流转"] = JsonNode.Parse("""[{"id":"oj8yEV","style":15,"text":"已完成"}]""") }));
            throw new Exception("Mismatched option label accepted");
        }
        catch (ApiException ex) { Check(ex.StatusCode == 400, "Changing option text without matching ID is rejected"); }
        return checks;
    }

    private sealed class Remote : IBreezaryRemote
    {
        public JsonObject Values = new() { ["状态"] = "待处理", ["备注"] = "初始备注" };
        public JsonObject? LastPatch;
        public int Reads, Writes;
        public bool FailWrite, Deleted;
        public Task<IReadOnlyList<BreezaryRow>> ReadAllAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Reads++;
            return Task.FromResult<IReadOnlyList<BreezaryRow>>(Deleted ? [] : [new()
            { RecordId = "r1", FileId = "file", SheetId = "sheet", Remote = (JsonObject)Values.DeepClone() }]);
        }
        public Task<JsonObject> UpdateAsync(string id, JsonObject patch, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Writes++;
            if (FailWrite) throw new ApiException(502, "simulated Tencent failure");
            LastPatch = (JsonObject)patch.DeepClone();
            Values = BreezaryMerge.Merge(Values, patch);
            return Task.FromResult((JsonObject)patch.DeepClone());
        }
    }
}
