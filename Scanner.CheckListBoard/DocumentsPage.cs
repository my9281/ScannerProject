using Scanner.CheckListBoard.Services;

namespace Scanner.CheckListBoard;

public sealed class DocumentsPage : ContentPage
{
    private readonly AccountClient accounts;
    private readonly Picker picker = new() { Title = "选择文档", ItemDisplayBinding = new Binding(nameof(BoardDocument.Title)) };
    private readonly Label status = new() { TextColor = Colors.DarkRed };
    private readonly WebView reader = new();
    private readonly Button refresh = new() { Text = "刷新目录" };
    private int generation;
    private bool loading;

    public DocumentsPage(AccountClient accounts)
    {
        this.accounts = accounts;
        Title = "操作文档";
        BackgroundColor = Colors.White;
        var header = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8 };
        header.Add(picker, 0); header.Add(refresh, 1);
        var layout = new Grid { Padding = 12, RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star) }, RowSpacing = 8 };
        layout.Add(header, 0, 0); layout.Add(status, 0, 1); layout.Add(reader, 0, 2);
        Content = layout;
        picker.SelectedIndexChanged += async (_, _) => { if (!loading && picker.SelectedItem is BoardDocument item) await ShowAsync(item.Path); };
        refresh.Clicked += async (_, _) => await LoadAsync();
        reader.Navigating += OnNavigating;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!accounts.HasPermission("checklist.read")) { reader.Source = null; status.Text = "登录已过期，请返回登录。"; return; }
        if (picker.ItemsSource is null) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        refresh.IsEnabled = false;
        loading = true;
        status.Text = "正在加载目录……";
        try
        {
            var entries = await accounts.DownloadDocumentsAsync();
            picker.ItemsSource = entries;
            picker.SelectedItem = entries.FirstOrDefault(item => item.Path == "sop/README.md") ?? entries.FirstOrDefault();
            if (picker.SelectedItem is BoardDocument selected) await ShowAsync(selected.Path);
            else { reader.Source = null; status.Text = "暂无文档，请联系管理员发布服务端文档资源。"; }
        }
        catch (Exception error) { reader.Source = null; status.Text = error.Message; }
        finally { loading = false; refresh.IsEnabled = true; }
    }

    private async Task ShowAsync(string path)
    {
        int current = ++generation;
        status.Text = "正在加载文档……";
        try
        {
            var document = await accounts.DownloadDocumentAsync(path);
            if (current != generation) return;
            reader.Source = new HtmlWebViewSource { Html = document.Html, BaseUrl = AccountClient.ValidateServer(accounts.ServerAddress).AbsoluteUri };
            status.Text = "";
        }
        catch (Exception error) { if (current == generation) { reader.Source = null; status.Text = error.Message; } }
    }

    private async void OnNavigating(object? sender, WebNavigatingEventArgs args)
    {
        if (args.Url is "about:blank" || args.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;
        if (args.Url == AccountClient.ValidateServer(accounts.ServerAddress).AbsoluteUri) return;
        args.Cancel = true;
        if (!Uri.TryCreate(AccountClient.ValidateServer(accounts.ServerAddress), args.Url, out var uri)) return;
        var server = AccountClient.ValidateServer(accounts.ServerAddress);
        bool local = uri.Scheme == server.Scheme && uri.Authority == server.Authority;
        try
        {
            string path = QueryPath(uri);
            if (local && uri.AbsolutePath.EndsWith("/documents.html", StringComparison.Ordinal) && path.Length > 0)
                await ShowAsync(path);
            else if (local && uri.AbsolutePath.EndsWith("/api/documents/file", StringComparison.Ordinal) && path.Length > 0)
            {
                status.Text = "正在下载附件……";
                byte[] bytes = await accounts.DownloadDocumentFileAsync(path);
                string directory = System.IO.Path.Combine(FileSystem.CacheDirectory, "documents", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                string file = System.IO.Path.Combine(directory, System.IO.Path.GetFileName(path));
                await File.WriteAllBytesAsync(file, bytes);
                await Share.Default.RequestAsync(new ShareFileRequest { Title = "文档附件", File = new ShareFile(file) });
                status.Text = "";
            }
            else if (uri.Scheme == "https" && !local) await Launcher.Default.OpenAsync(uri);
        }
        catch (Exception error) { status.Text = error.Message; }
    }

    private static string QueryPath(Uri uri)
    {
        foreach (string part in uri.Query.TrimStart('?').Split('&'))
        {
            string[] pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0] == "path") return Uri.UnescapeDataString(pair[1]);
        }
        return "";
    }
}
