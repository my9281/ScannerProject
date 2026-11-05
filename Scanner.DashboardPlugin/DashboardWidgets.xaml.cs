using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Scanner.DashboardPlugin
{
    public partial class DashboardWidgets : UserControl
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        private static readonly TimeZoneInfo Nj = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        private static readonly TimeZoneInfo Beijing = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        public static readonly DependencyProperty UiCultureProperty = DependencyProperty.Register(
            nameof(UiCulture), typeof(string), typeof(DashboardWidgets),
            new PropertyMetadata("zh-CN", (owner, _) => ((DashboardWidgets)owner).RenderDashboard()));
        public string UiCulture
        {
            get => (string)GetValue(UiCultureProperty);
            set => SetValue(UiCultureProperty, value);
        }
        private CultureInfo Culture => CultureInfo.GetCultureInfo(UiCulture is "en-US" or "es-ES" ? UiCulture : "zh-CN");
        private string L(string chinese, string english, string spanish) => UiCulture switch
        {
            "en-US" => english, "es-ES" => spanish, _ => chinese
        };
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private CancellationTokenSource lifetime;
        private DateTimeOffset nextRefresh = DateTimeOffset.MinValue;
        private DateTime sunDay, displayedDay, sunrise, sunset, observedAt;
        private double temperature;
        private int weatherCode;
        private bool fetching, hasWeather, refreshFailed;
        public DashboardWidgets(Version hostVersion)
        {
            InitializeComponent();
            VersionText.Text = "v" + hostVersion;
            SetResourceReference(UiCultureProperty, "DashboardCulture");
            RenderDashboard();
            timer.Tick += Tick;
        }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (lifetime != null) return;
            lifetime = new CancellationTokenSource();
            timer.Start();
            Tick(null, EventArgs.Empty);
        }
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            timer.Stop();
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
        }
        private async void Tick(object sender, EventArgs e)
        {
            var utc = DateTimeOffset.UtcNow;
            var day = TimeZoneInfo.ConvertTime(utc, Nj).Date;
            if (displayedDay != day)
            {
                displayedDay = day;
                nextRefresh = DateTimeOffset.MinValue;
                refreshFailed = false;
            }
            RenderDashboard();
            if (!fetching && lifetime != null && utc >= nextRefresh)
                await RefreshAsync(lifetime.Token);
        }
        private void RenderDashboard()
        {
            if (NjClock == null) return;
            var utc = DateTimeOffset.UtcNow;
            var nj = TimeZoneInfo.ConvertTime(utc, Nj);
            var bj = TimeZoneInfo.ConvertTime(utc, Beijing);
            bool dst = Nj.IsDaylightSavingTime(nj);
            NjClock.Text = L("新泽西 ", "NJ ", "NJ ") + nj.ToString("MM-dd ddd HH:mm:ss", Culture);
            BeijingClock.Text = L("北京 ", "Beijing ", "Pekín ") + bj.ToString("MM-dd ddd HH:mm:ss", Culture);
            Zones.Text = (dst ? L("EDT UTC−04 夏令时", "EDT UTC−04 daylight time", "EDT UTC−04 horario de verano")
                : L("EST UTC−05 冬令时", "EST UTC−05 standard time", "EST UTC−05 horario estándar"))
                + L(" · 北京 UTC+08 无夏令时", " · Beijing UTC+08, no DST", " · Pekín UTC+08, sin cambio estacional");
            SunTitle.Text = L("日出 / 日落 · 新泽西 Newark", "Sunrise / sunset · Newark, NJ", "Amanecer / ocaso · Newark, NJ");
            WeatherTitle.Text = L("天气 · 新泽西 Newark", "Weather · Newark, NJ", "Tiempo · Newark, NJ");
            WeatherPanel.ToolTip = L("来源 Open-Meteo · 每15分钟刷新；天气为模型估算",
                "Source: Open-Meteo · refreshed every 15 minutes; model-estimated weather",
                "Fuente: Open-Meteo · actualización cada 15 minutos; tiempo estimado por modelo");
            VersionTitle.Text = L("当前版本", "Current version", "Versión actual");
            VersionHint.Text = L("启动时由现有自动更新逻辑检查更新", "Updates checked at startup", "Actualizaciones al iniciar");
            Sun.Text = sunDay == nj.Date
                ? string.Format(Culture, L("日出 {0:HH:mm}   日落 {1:HH:mm}", "Rise {0:HH:mm}   Set {1:HH:mm}", "Sale {0:HH:mm}   Se pone {1:HH:mm}"), sunrise, sunset)
                : refreshFailed ? L("日出日落暂不可用", "Sun times unavailable", "Horario solar no disponible")
                : L("正在查询当日日出日落…", "Loading today's sun times…", "Cargando horario solar de hoy…");
            SunDate.Text = nj.ToString("yyyy-MM-dd", Culture) + L(" · 新泽西当地时间", " · NJ local time", " · Hora local de NJ");
            Weather.Text = hasWeather
                ? string.Format(Culture, "{0}  {1:0.#} °C / {2:0.#} °F", Describe(weatherCode), temperature, temperature * 1.8 + 32)
                : refreshFailed ? L("天气暂不可用", "Weather unavailable", "Tiempo no disponible") : L("正在查询…", "Loading…", "Cargando…");
            WeatherTime.Text = refreshFailed
                ? L("联网失败 · 1分钟后重试（已有数据可能过期）", "Offline · retry in 1 min; data may be stale", "Sin conexión · reintento en 1 min; datos posiblemente antiguos")
                : hasWeather ? string.Format(Culture, L("数据 {0:MM-dd HH:mm} · 新泽西时间", "Updated {0:MM-dd HH:mm} · NJ time", "Datos {0:MM-dd HH:mm} · Hora NJ"), observedAt) : "";
        }
        private async Task RefreshAsync(CancellationToken token)
        {
            fetching = true;
            try
            {
                const string url = "https://api.open-meteo.com/v1/forecast?latitude=40.7357&longitude=-74.1724&current=temperature_2m,weather_code&daily=sunrise,sunset&timezone=America%2FNew_York&forecast_days=1";
                using var response = await Http.GetAsync(url, token);
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                var root = json.RootElement;
                var daily = root.GetProperty("daily");
                var day = DateTime.ParseExact(daily.GetProperty("time")[0].GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var rise = DateTime.Parse(daily.GetProperty("sunrise")[0].GetString(), CultureInfo.InvariantCulture);
                var set = DateTime.Parse(daily.GetProperty("sunset")[0].GetString(), CultureInfo.InvariantCulture);
                var current = root.GetProperty("current");
                var degrees = current.GetProperty("temperature_2m").GetDouble();
                var code = current.GetProperty("weather_code").GetInt32();
                var observed = DateTime.Parse(current.GetProperty("time").GetString(), CultureInfo.InvariantCulture);
                token.ThrowIfCancellationRequested();
                sunDay = day; sunrise = rise; sunset = set; temperature = degrees; weatherCode = code; observedAt = observed;
                hasWeather = true; refreshFailed = false;
                nextRefresh = DateTimeOffset.UtcNow.AddMinutes(15);
                RenderDashboard();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException || ex is JsonException || ex is InvalidOperationException || ex is FormatException || ex is System.Collections.Generic.KeyNotFoundException || ex is ArgumentException)
            {
                if (token.IsCancellationRequested) return;
                refreshFailed = true;
                nextRefresh = DateTimeOffset.UtcNow.AddMinutes(1);
                RenderDashboard();
            }
            finally { fetching = false; }
        }
        private string Describe(int code) => code switch
        {
            0 => L("晴", "Clear", "Despejado"), 1 => L("大致晴朗", "Mainly clear", "Mayormente despejado"),
            2 => L("局部多云", "Partly cloudy", "Parcialmente nublado"), 3 => L("阴", "Overcast", "Cubierto"),
            45 or 48 => L("雾", "Fog", "Niebla"), 51 or 53 or 55 => L("毛毛雨", "Drizzle", "Llovizna"),
            56 or 57 => L("冻毛毛雨", "Freezing drizzle", "Llovizna helada"),
            61 or 63 or 65 => L("雨", "Rain", "Lluvia"), 66 or 67 => L("冻雨", "Freezing rain", "Lluvia helada"),
            71 or 73 or 75 or 77 => L("雪", "Snow", "Nieve"), 80 or 81 or 82 => L("阵雨", "Rain showers", "Chubascos"),
            85 or 86 => L("阵雪", "Snow showers", "Chubascos de nieve"), 95 => L("雷雨", "Thunderstorm", "Tormenta"),
            96 or 99 => L("雷雨伴冰雹", "Thunderstorm with hail", "Tormenta con granizo"),
            _ => string.Format(Culture, L("未知天气（{0}）", "Unknown weather ({0})", "Tiempo desconocido ({0})"), code)
        };
    }
}
