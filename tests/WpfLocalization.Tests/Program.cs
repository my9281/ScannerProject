using System.IO;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Scanner.WPF;
using Scanner.WPF.Helpers;
using Scanner.Helpers.Services;
internal static class Program
{
 [STAThread]
 static void Main(string[] args)
 {
  var root = args[0];
  var app = new Scanner.WPF.App(); app.InitializeComponent();
  var x = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
  Dictionary<string,string> baseline = null;
  foreach(var language in new[]{"zh-CN","en-US","es-ES"})
  {
   var elements = XDocument.Load(Path.Combine(root,"SharedAssets","Languages","Language."+language+".xaml")).Root.Elements().Where(e=>e.Attribute(x+"Key")!=null).ToArray();
   var entries=elements.ToDictionary(e=>(string)e.Attribute(x+"Key"),e=>e.Value);
   if(baseline==null) baseline=entries;
   if(!baseline.Keys.ToHashSet().SetEquals(entries.Keys)) throw new Exception("Language key mismatch");
   foreach(var key in entries.Keys)
   {
    var placeholders = Regex.Matches(entries[key],@"\{(\d+)(?:[^}]*)\}").Select(m=>m.Groups[1].Value).Order().ToArray();
    var expected = Regex.Matches(baseline[key],@"\{(\d+)(?:[^}]*)\}").Select(m=>m.Groups[1].Value).Order().ToArray();
    if(!placeholders.SequenceEqual(expected)) throw new Exception("Format mismatch "+key);
    if(language!="zh-CN" && key.StartsWith("Wpf") && Regex.IsMatch(entries[key],@"[\u4e00-\u9fff]")) throw new Exception("Untranslated "+key);
   }
   UiText.ChangeLanguage(language);
   var composition=typeof(MainWindow).Assembly.GetType("Scanner.WPF.Controllers.DesktopComposition");
   using var provider=(IDisposable)composition.GetMethod("Build",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,null);
   var services=(IServiceProvider)provider;
   var api=(ShelvedPalletApiService)services.GetService(typeof(ShelvedPalletApiService));
   var windows=new Window[]{(Window)services.GetService(typeof(MainWindow)),(Window)services.GetService(typeof(OutboundInspectionWindow)),new GoodAreaWindow(api),new SkuSnBatchPicker(api),new AutomaticMatchingWindow(new ChecklistDataCache(),api),new BatchLabelWindow(),new RegistrationRepairWindow(new ChecklistDataCache(),api)};
   foreach(var window in windows)
   {
    if(window.Icon==null) throw new Exception("Missing icon "+window.GetType().Name);
    var content=(FrameworkElement)window.Content;
    int width=(int)window.Width-16,height=(int)window.Height-39;
    content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();
    var visual=new DrawingVisual();
    using(var dc=visual.RenderOpen()) { dc.DrawRectangle(window.Background,null,new Rect(0,0,width,height));dc.DrawRectangle(new VisualBrush(content),null,new Rect(0,0,width,height)); }
    var bmp=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bmp.Render(visual);
    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));
    var dir=Path.Combine(AppContext.BaseDirectory,"previews");Directory.CreateDirectory(dir);
    using var output=File.Create(Path.Combine(dir,window.GetType().Name+"-"+language+".png"));encoder.Save(output);
   }
   Console.WriteLine("PASS: "+language+" resources, placeholders, icons and 7 window renders");
  }
 }
}
