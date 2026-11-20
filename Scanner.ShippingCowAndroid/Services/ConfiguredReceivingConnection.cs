using System.Net;
using Scanner.Server.Model;

namespace Scanner.ShippingCowAndroid.Services;

/// <summary>Configured login and one-time renewal; never reassign an offline scan to a new identity.</summary>
public sealed class ConfiguredReceivingConnection(ICarrierReceivingApi api, AppSettings settings)
{
    public ReceivingLogin? Session { get; private set; }
    private bool Matches(CarrierReceiptContext? context) => context is not null && context.Server == settings.UploadBaseUrl.AbsoluteUri
        && string.Equals(context.Username, settings.ReceivingUsername, StringComparison.OrdinalIgnoreCase)
        && context.UserId != 0 && context.DomainId > 0 && !string.IsNullOrWhiteSpace(context.SiteId);

    public bool Restore(ReceivingLogin? cached)
    {
        if (cached is null || string.IsNullOrWhiteSpace(cached.Token) || !Matches(cached.Context)) return false;
        Session = cached;
        return true;
    }
    private void RequireConfiguration()
    {
        if (string.IsNullOrWhiteSpace(settings.ReceivingUsername) || string.IsNullOrEmpty(settings.ReceivingPassword))
            throw new InvalidOperationException("请在 appsettings.xml 填写 ReceivingUsername 和 ReceivingPassword 后重新打包安装。");
    }
    public async Task<ReceivingLogin> Login(CancellationToken ct)
    {
        RequireConfiguration();
        ReceivingLogin result;
        try { result = await api.Login(settings.ReceivingUsername, settings.ReceivingPassword, ct); }
        catch (CarrierReceivingApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        { Session = null; throw; }
        if (string.IsNullOrWhiteSpace(result.Token) || !Matches(result.Context))
        { Session = null; throw new InvalidOperationException("服务器返回的账号或站点身份与配置不一致，未更改收货记录。"); }
        Session = result;
        return result;
    }
    public async Task<ReceivingLogin> Connect(CancellationToken ct)
    {
        RequireConfiguration();
        if (Session is null || Session.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30)) return await Login(ct);
        try
        {
            var context = await api.Context(Session.Token, ct);
            if (!Matches(context) || context.UserId != Session.Context.UserId || context.DomainId != Session.Context.DomainId)
            { Session = null; throw new InvalidOperationException("收货账号身份已变更，请核对配置与原待上传记录。"); }
            Session = Session with { Context = context };
            return Session;
        }
        catch (CarrierReceivingApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            Session = null;
            return await Login(ct);
        }
        catch (CarrierReceivingApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        { Session = null; throw; }
    }
    private static void CheckOwner(CarrierReceiptBatch batch, ReceivingLogin session)
    {
        if (batch.Items.Any(s => s.OperatorUserId != session.Context.UserId || s.DomainId != session.Context.DomainId))
            throw new InvalidOperationException("待上传记录属于原账号，请将配置改回原账号并完成上传，不能转移记录。");
    }
    public async Task<ReceivingLogin> PrepareUpload(CancellationToken ct)
    {
        RequireConfiguration();
        if (Session is null || Session.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30)) return await Login(ct);
        return Session;
    }
    private async Task<CarrierReceiptAcknowledgement> Send(ReceivingLogin session, CarrierReceiptBatch batch, CancellationToken ct)
    {
        try { return await api.Upload(session.Token, batch, ct); }
        catch (CarrierReceivingApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        { Session = null; throw; }
    }
    public async Task<CarrierReceiptAcknowledgement> Upload(CarrierReceiptBatch batch, CancellationToken ct)
    {
        RequireConfiguration();
        if (Session is null || Session.ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30)) await Login(ct);
        CheckOwner(batch, Session!);
        try { return await Send(Session!, batch, ct); }
        catch (CarrierReceivingApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var renewed = await Login(ct);
            CheckOwner(batch, renewed);
            // A second rejection propagates. The durable queue still retains the original scan IDs.
            return await Send(renewed, batch, ct);
        }
        catch (CarrierReceivingApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        { Session = null; throw; }
    }
}
