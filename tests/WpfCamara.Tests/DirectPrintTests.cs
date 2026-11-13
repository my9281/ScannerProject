using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WPF.Camara;
using ZXing;
using ZXing.Common;

internal static class DirectPrintTests
{
    public static void Run()
    {
        var rows = ReceivingForecast.LoadDefault().Rows;
        foreach (int dpi in new[] { 203, 300 })
        foreach (int line in new[] { 1, 9, 34, 45, 58, 62, 66 })
        {
            var row = rows.Single(x => x.Line == line);
            var label = ReceivingLabel.Create(row, 1, row.Quantity);
            var data = ZplLabelPrinter.Encode(label, dpi);
            string zpl = Encoding.ASCII.GetString(data);
            if (!zpl.StartsWith($"^XA^PW{4 * dpi}^LL{6 * dpi}^LH0,0") || !zpl.EndsWith("^FS^PQ1^XZ")) throw new Exception("ZPL尺寸或单张数量错误");
            if (Regex.Matches(zpl,"\\^XA").Count != 1 || Regex.Matches(zpl,"\\^XZ").Count != 1) throw new Exception("一个任务中包含多张标签");
            var strips = Regex.Matches(zpl, @"\^FO0,(\d+)\^GFA,(\d+),(\d+),(\d+),([0-9A-F]+)\^FS");
            if (strips.Count < 2) throw new Exception("必须分块，不能发送超大的单个图像指令");
            int stride = (4*dpi+7)/8;
            var raster = new byte[stride * 6*dpi];
            int nextY = 0;
            foreach (Match strip in strips)
            {
                int y=int.Parse(strip.Groups[1].Value), bytes=int.Parse(strip.Groups[2].Value);
                if(y!=nextY || bytes>4096 || bytes<1 || int.Parse(strip.Groups[3].Value)!=bytes || int.Parse(strip.Groups[4].Value)!=stride || bytes%stride!=0) throw new Exception("图像块位置/长度超限或不连续");
                var part=Convert.FromHexString(strip.Groups[5].Value);
                if(part.Length!=bytes)throw new Exception("图像块十六进制长度错误");
                part.CopyTo(raster,y*stride);
                nextY+=bytes/stride;
            }
            if (nextY!=6*dpi) throw new Exception("标签下半部分缺失");
            if (!raster.Any(x => x != 0) || !raster.Any(x => x == 0)) throw new Exception("ZPL图像全黑或全白");
            var expectedBitmap = new RenderTargetBitmap(4*dpi,6*dpi,dpi,dpi,PixelFormats.Pbgra32);
            expectedBitmap.Render(label);
            var expectedPixels = new byte[4*dpi*6*dpi*4];
            expectedBitmap.CopyPixels(expectedPixels,4*dpi*4,0);
            var restored = new byte[4*dpi*6*dpi*4];
            for(int y=0;y<6*dpi;y++)for(int x=0;x<4*dpi;x++)
            {
                int pos=(y*4*dpi+x)*4;
                bool black=(raster[y*stride+x/8] & (0x80>>(x%8)))!=0;
                int luminance=(expectedPixels[pos]*114+expectedPixels[pos+1]*587+expectedPixels[pos+2]*299)/1000+255-expectedPixels[pos+3];
                if(black!=(luminance<128))throw new Exception("分块拼接后与完整预览不一致");
                restored[pos]=restored[pos+1]=restored[pos+2]=(byte)(black?0:255);restored[pos+3]=255;
            }
            if(line==109 && dpi==203)
            {
                Directory.CreateDirectory("artifacts/camara/receiving");
                var image=BitmapSource.Create(4*dpi,6*dpi,dpi,dpi,PixelFormats.Bgra32,null,restored,4*dpi*4);
                var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));
                using var stream=File.Create("artifacts/camara/receiving/row-109-zpl-fixed.png");png.Save(stream);
            }
            foreach (var image in Images(label))
            {
                var p = image.TransformToAncestor(label).Transform(new Point(0,0));
                int left=(int)(p.X*dpi/96), top=(int)(p.Y*dpi/96);
                int w=(int)Math.Round(image.ActualWidth*dpi/96), h=(int)Math.Round(image.ActualHeight*dpi/96);
                var rgb = new byte[w*h*3];
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    bool black=(raster[(top+y)*stride+(left+x)/8] & (0x80>>((left+x)%8)))!=0;
                    int pos=(y*w+x)*3; rgb[pos]=rgb[pos+1]=rgb[pos+2]=(byte)(black?0:255);
                }
                var reader = new BarcodeReaderGeneric { Options = new DecodingOptions { TryHarder=true, PossibleFormats=[image.Name=="ProductBarcode"?BarcodeFormat.CODE_128:BarcodeFormat.QR_CODE] } };
                var decoded=reader.Decode(new RGBLuminanceSource(rgb,w,h,RGBLuminanceSource.BitmapFormat.RGB24));
                if(decoded?.Text!=(image.Name=="ProductBarcode"?row.Barcode:row.Sku)) throw new Exception($"ZPL栅格解码失败：{dpi}DPI 行{line} {image.Name}");
            }
        }
        var payload=Enumerable.Range(0,17000).Select(x=>(byte)(x%251)).ToArray();
        var received=new List<byte>();
        ZplLabelPrinter.WriteInChunks(payload, chunk =>
        {
            if(chunk.Length>4096)throw new Exception("单次发送过大");
            int accepted=Math.Max(1,chunk.Length/2);received.AddRange(chunk.Take(accepted));return accepted;
        });
        if(!payload.SequenceEqual(received))throw new Exception("短写后丢字节或重复发送");
        try { ZplLabelPrinter.WriteInChunks(payload,_=>0);throw new Exception("零字节写入未停止"); }catch(IOException) { }
        SequenceTests().GetAwaiter().GetResult();
        Console.WriteLine("PASS: 203/300DPI分块ZPL每块<=4096字节，完整覆盖全页且与原图逐像素一致；条码解码；单张任务；传输短写无丢失/重复；零写入停止；发送顺序及取消。");
    }

    private static IEnumerable<Image> Images(DependencyObject node)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)
        {
            var child=VisualTreeHelper.GetChild(node,i);
            if(child is Image image) yield return image;
            foreach(var descendant in Images(child)) yield return descendant;
        }
    }

    private static async Task SequenceTests()
    {
        var sent = new List<int>();
        int active = 0;
        int result = await LabelSendSequence.RunAsync(4, async i =>
        {
            if (++active != 1) throw new Exception("发送重叠");
            await Task.Yield(); sent.Add(i); active--;
        }, _ => {}, CancellationToken.None);
        if (result != 4 || !sent.SequenceEqual(new[] { 0,1,2,3 })) throw new Exception("未逐张顺序发送");
        using var cancel = new CancellationTokenSource();sent.Clear();
        result = await LabelSendSequence.RunAsync(8, i => { sent.Add(i); return Task.CompletedTask; }, n => { if (n == 2) cancel.Cancel(); }, cancel.Token);
        if (result != 2 || sent.Count != 2) throw new Exception("停止后仍继续发送");
        sent.Clear();
        try
        {
            await LabelSendSequence.RunAsync(8, i => { sent.Add(i); if (i == 2) throw new InvalidOperationException("模拟打印失败"); return Task.CompletedTask; }, _ => {}, CancellationToken.None);
            throw new Exception("发送失败未上抛");
        }
        catch (InvalidOperationException) { }
        if (!sent.SequenceEqual(new[] {0,1,2})) throw new Exception("失败后仍继续或重复发送");
    }
}
