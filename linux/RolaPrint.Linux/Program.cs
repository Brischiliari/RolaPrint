using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using RolaPrint.Portable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace RolaPrint.Linux;

static class Program
{
    [STAThread] public static void Main(string[] args) =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
}

sealed class App : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark; }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}

sealed class Bridge : IDisposable
{
    readonly Process process;
    readonly SemaphoreSlim gate = new(1);
    public Bridge()
    {
        var start = new ProcessStartInfo("/usr/bin/python3") { RedirectStandardInput = true,
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "portal.py"));
        process = Process.Start(start) ?? throw new IOException("Não foi possível iniciar o portal.");
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
        process.BeginErrorReadLine();
    }
    public async Task<JsonElement> Send(object command)
    {
        await gate.WaitAsync();
        try
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(command));
            await process.StandardInput.FlushAsync();
            string? line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(135));
            if (line == null) throw new IOException("Portal encerrado. Instale as dependências do pacote .deb.");
            using var json = JsonDocument.Parse(line);
            var result = json.RootElement.Clone();
            if (!result.GetProperty("ok").GetBoolean()) throw new IOException(result.GetProperty("error").GetString());
            return result;
        }
        finally { gate.Release(); }
    }
    public async Task<RgbFrame> Frame(string command = "frame")
    {
        var r = await Send(new { command });
        return new RgbFrame(r.GetProperty("width").GetInt32(), r.GetProperty("height").GetInt32(),
            Convert.FromBase64String(r.GetProperty("rgb").GetString()!));
    }
    public void Dispose()
    {
        try { process.StandardInput.Close(); if (!process.WaitForExit(1500)) process.Kill(true); } catch { }
        process.Dispose();
    }
}

sealed class MainWindow : Window
{
    readonly TextBlock status = new() { Text = "Compartilhe um monitor e marque a área do documento na prévia.", TextWrapping = TextWrapping.Wrap };
    readonly Image preview = new() { Stretch = Stretch.Uniform };
    readonly Button select = new() { Content = "1  Selecionar tela" }, start = new() { Content = "2  Iniciar captura", IsEnabled = false },
        save = new() { Content = "3  Salvar PNG", IsEnabled = false };
    Bridge? bridge;
    RgbFrame? source;
    WriteableBitmap? displayed;
    readonly List<RgbFrame> parts = new();
    PixelRect region;
    Point? origin;
    CancellationTokenSource? capture;
    Window? stopPanel;
    bool busy;

    public MainWindow()
    {
        Title = "RolaPrint • Linux"; Width = 920; Height = 740; MinWidth = 600; MinHeight = 450;
        Background = new SolidColorBrush(Color.Parse("#141414"));
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12 };
        foreach (var button in new[] { select, start, save }) { button.Padding = new Thickness(22, 14); buttons.Children.Add(button); }
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(28) };
        var title = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        title.Children.Add(new TextBlock { Text = "RolaPrint", FontSize = 36, FontWeight = FontWeight.Bold });
        title.Children.Add(new TextBlock { Text = "Uma captura. Todo o conteúdo.", Margin = new Thickness(0, 8, 0, 0) });
        layout.Children.Add(title); Grid.SetRow(buttons, 1); layout.Children.Add(buttons);
        status.Margin = new Thickness(0, 18); Grid.SetRow(status, 2); layout.Children.Add(status);
        var surface = new Border { Background = new SolidColorBrush(Color.Parse("#222222")), Child = preview, ClipToBounds = true };
        Grid.SetRow(surface, 3); layout.Children.Add(surface); Content = layout;
        select.Click += async (_, _) => await Select();
        start.Click += async (_, _) => await Capture();
        save.Click += async (_, _) => await Save();
        preview.PointerPressed += (_, e) => { if (!busy && source != null) origin = e.GetPosition(preview); };
        preview.PointerReleased += (_, e) =>
        {
            if (origin == null || source == null || busy) return;
            var end = e.GetPosition(preview); var a = Map(origin.Value); var b = Map(end); origin = null;
            region = new PixelRect(Math.Min(a.X,b.X), Math.Min(a.Y,b.Y), Math.Abs(a.X-b.X), Math.Abs(a.Y-b.Y));
            start.IsEnabled = region.Width >= 100 && region.Height >= 150;
            status.Text = start.IsEnabled ? $"Área: {region.Width} × {region.Height}. Deixe o documento visível e clique em Iniciar." : "Marque uma área maior na prévia.";
        };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) capture?.Cancel(); };
        Closed += (_, _) => { capture?.Cancel(); stopPanel?.Close(); bridge?.Dispose(); displayed?.Dispose(); };
    }

    PixelPoint Map(Point point)
    {
        var s = source!; double scale = Math.Min(preview.Bounds.Width/s.Width, preview.Bounds.Height/s.Height);
        double x = (point.X-(preview.Bounds.Width-s.Width*scale)/2)/scale;
        double y = (point.Y-(preview.Bounds.Height-s.Height*scale)/2)/scale;
        return new PixelPoint((int)Math.Clamp(x,0,s.Width), (int)Math.Clamp(y,0,s.Height));
    }
    async Task Select()
    {
        select.IsEnabled = start.IsEnabled = save.IsEnabled = false;
        try
        {
            bridge?.Dispose(); bridge = new Bridge();
            status.Text = "Autorize o compartilhamento da tela e o controle do mouse no seletor do sistema.";
            WindowState=WindowState.Minimized;
            source = await bridge.Frame("open"); region = default;
            ShowFrame(source); status.Text = "Arraste na prévia para marcar somente a área que rola. Evite barras e painéis.";
        }
        catch (Exception ex) { status.Text = "Não foi possível abrir a captura: " + ex.Message; }
        finally { WindowState=WindowState.Normal; Activate(); select.IsEnabled = true; }
    }
    static RgbFrame Crop(RgbFrame frame, PixelRect r)
    {
        if (r.Right > frame.Width || r.Bottom > frame.Height) throw new IOException("A resolução mudou. Selecione a tela novamente.");
        byte[] bytes = new byte[checked(r.Width*r.Height*3)];
        for(int y=0;y<r.Height;y++) Buffer.BlockCopy(frame.Data,((r.Y+y)*frame.Width+r.X)*3,bytes,y*r.Width*3,r.Width*3);
        return new RgbFrame(r.Width,r.Height,bytes);
    }
    async Task Capture()
    {
        if (bridge == null || busy) return;
        busy = true; select.IsEnabled = start.IsEnabled = save.IsEnabled = false;
        capture = new CancellationTokenSource(); var token = capture.Token;
        Alignment.CancellationRequested = () => token.IsCancellationRequested;
        parts.Clear();
        var stop = new Button { Content = "Parar e ver resultado", Padding = new Thickness(18) };
        var progress=new TextBlock {Text="Preparando captura…",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)};
        var panel=new StackPanel();panel.Children.Add(progress);panel.Children.Add(stop);
        EventHandler<AvaloniaPropertyChangedEventArgs> update=(_,e)=> {if(e.Property==TextBlock.TextProperty)progress.Text=status.Text;};
        status.PropertyChanged+=update;
        stopPanel = new Window { Title = "RolaPrint • captura", Width = 320, Height = 165, Topmost = true, Content = panel };
        stop.Click += (_, _) => capture.Cancel();
        stopPanel.Closing += (_, _) => capture.Cancel();
        stopPanel.Show(); WindowState = WindowState.Minimized;
        try
        {
            await Task.Delay(1800,token);
            var previous = Crop(await bridge.Frame(),region); parts.Add(previous);
            int height = previous.Height;
            while (!token.IsCancellationRequested)
            {
                // Test motion before each scroll so video pixels are excluded from alignment.
                await Task.Delay(65,token);
                var still = Crop(await bridge.Frame(),region);
                var oldMotion = Alignment.DetectMotion(previous,still);
                await bridge.Send(new {command="scroll",x=region.X+region.Width/2,y=region.Y+region.Height/2,steps=2});
                await Task.Delay(180,token);
                var next = Crop(await bridge.Frame(),region);
                await Task.Delay(65,token);
                var nextStill = Crop(await bridge.Frame(),region);
                var motion = Alignment.DetectMotion(next,nextStill);
                int shift = await Task.Run(() => {
                    int value = Alignment.Match(still,next,oldMotion,motion);
                    if(value<0) value=Alignment.Anchors(still,next,oldMotion,motion);
                    return value;
                },token);
                if (shift < 0)
                {
                    // Attempt to undo the scroll, then pause. Recovery is not assumed successful.
                    await bridge.Send(new {command="scroll",x=region.X+region.Width/2,y=region.Y+region.Height/2,steps=-2});
                    status.Text="Alinhamento incerto. Coleta pausada; pare para salvar o resultado.";
                    await Task.Delay(Timeout.Infinite,token);
                }
                if (shift > 0)
                {
                    if ((long)(height+shift)*region.Width>40_000_000 || height+shift>30000)
                    { status.Text="Limite de imagem atingido. Pare para salvar."; await Task.Delay(Timeout.Infinite,token); }
                    parts.Add(next.Crop(next.Height-shift,shift)); height += shift;
                }
                previous = nextStill;
                status.Text=$"Capturando: {height:N0} pixels. Use o botão Parar para finalizar.";
            }
        }
        catch(OperationCanceledException) { }
        catch(Exception ex) { status.Text="Captura interrompida: "+ex.Message+" O conteúdo coletado pode ser salvo."; }
        finally
        {
            status.PropertyChanged-=update;
            busy=false; stopPanel?.Close(); stopPanel=null; WindowState=WindowState.Normal; Activate();
            select.IsEnabled=true; start.IsEnabled=true; save.IsEnabled=parts.Count>0;
            source=null;
            if(parts.Count>0) ShowFrame(Assemble());
            capture.Dispose(); capture=null;
        }
    }
    RgbFrame Assemble()
    {
        int width=parts[0].Width,height=parts.Sum(p=>p.Height); byte[] bytes=new byte[checked(width*height*3)]; int offset=0;
        foreach(var part in parts) { Buffer.BlockCopy(part.Data,0,bytes,offset,part.Data.Length); offset+=part.Data.Length; }
        return new RgbFrame(width,height,bytes);
    }
    static WriteableBitmap Bitmap(RgbFrame frame)
    {
        var bitmap=new WriteableBitmap(new PixelSize(frame.Width,frame.Height),new Vector(96,96),PixelFormat.Bgra8888,AlphaFormat.Opaque);
        byte[] row=new byte[frame.Width*4];
        using(var locked=bitmap.Lock()) for(int y=0;y<frame.Height;y++)
        {
            for(int x=0;x<frame.Width;x++) { int rgb=(y*frame.Width+x)*3,i=x*4; row[i]=frame.Data[rgb+2];row[i+1]=frame.Data[rgb+1];row[i+2]=frame.Data[rgb];row[i+3]=255; }
            Marshal.Copy(row,0,IntPtr.Add(locked.Address,y*locked.RowBytes),row.Length);
        }
        return bitmap;
    }
    void ShowFrame(RgbFrame frame) { var old=displayed; displayed=Bitmap(frame); preview.Source=displayed; old?.Dispose(); }
    async Task Save()
    {
        try
        {
            var file=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title="Salvar captura",SuggestedFileName=$"RolaPrint-{DateTime.Now:yyyyMMdd-HHmmss}.png",DefaultExtension="png",FileTypeChoices=new[]{new FilePickerFileType("PNG"){Patterns=new[]{"*.png"}}}});
            if(file==null)return;
            using var bitmap=Bitmap(Assemble()); using var output=await file.OpenWriteAsync(); bitmap.Save(output);
            status.Text="Imagem salva.";
        }
        catch(Exception ex) { status.Text="Não foi possível salvar: "+ex.Message; }
    }
    readonly record struct PixelPoint(int X,int Y);
}
