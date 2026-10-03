using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

internal static class Program {
 [STAThread] static void Main(string[] args) {
  CaptureEngine.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  using(var form=new MainWindow()) {
   if(args.Length==2 && args[0]=="--preview") {
    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();Application.DoEvents();
    using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,form.Size));b.Save(args[1],ImageFormat.Png);}return;
   }
   Application.Run(form);
  }
 }
}

internal static class Theme {
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
 public static void DarkTitle(Form form){try{int enabled=1;DwmSetWindowAttribute(form.Handle,20,ref enabled,4);}catch{}}
 public static Icon AppIcon(){using(var stream=typeof(Theme).Assembly.GetManifestResourceStream("RolaPrint.ico")){if(stream==null)return (Icon)SystemIcons.Application.Clone();using(var icon=new Icon(stream))return (Icon)icon.Clone();}}
 public static readonly Color Background=Color.FromArgb(15,15,17), Surface=Color.FromArgb(25,25,28), Text=Color.FromArgb(240,240,242), Muted=Color.FromArgb(153,153,160), Blue=Color.FromArgb(225,225,230);
 public static Button Button(string text,bool primary) {
  var b=new ModernButton(primary) {Text=text,Height=50,Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,BackColor=primary?Blue:Surface,ForeColor=primary?Background:Text,Cursor=Cursors.Hand,Font=new Font("Segoe UI",10,FontStyle.Bold),Margin=new Padding(0,0,12,12)};
  b.FlatAppearance.BorderSize=0;return b;
 }
}

internal sealed class ModernButton : Button {
 readonly bool primary;bool hover,pressed;
 public ModernButton(bool primary){this.primary=primary;DoubleBuffered=true;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);
  Rectangle r=new Rectangle(1,1,Math.Max(1,Width-3),Math.Max(1,Height-3));int radius=12;
  using(var path=new GraphicsPath()){
   path.AddArc(r.Left,r.Top,radius*2,radius*2,180,90);path.AddArc(r.Right-radius*2,r.Top,radius*2,radius*2,270,90);path.AddArc(r.Right-radius*2,r.Bottom-radius*2,radius*2,radius*2,0,90);path.AddArc(r.Left,r.Bottom-radius*2,radius*2,radius*2,90,90);path.CloseFigure();
   Color fill=!Enabled?Color.FromArgb(29,29,32):primary?(pressed?Color.FromArgb(185,185,193):hover?Color.White:Theme.Blue):(pressed?Color.FromArgb(45,45,50):hover?Color.FromArgb(40,40,45):Color.FromArgb(30,30,34));
   using(var brush=new SolidBrush(fill))e.Graphics.FillPath(brush,path);
   if(!primary||!Enabled)using(var pen=new Pen(Color.FromArgb(49,49,55)))e.Graphics.DrawPath(pen,path);
  }
  TextRenderer.DrawText(e.Graphics,Text,Font,r,!Enabled?Color.FromArgb(115,115,123):primary?Theme.Background:Theme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
  if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(8,8,Width-16,Height-16),Theme.Muted,BackColor);
 }
}

internal sealed class SelectionWindow : Form {
 Point start;bool dragging;Rectangle box;public Rectangle SelectedArea {get;private set;}
 public SelectionWindow() {
  FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;Bounds=SystemInformation.VirtualScreen;BackColor=Color.Black;Opacity=.35;TopMost=true;Cursor=Cursors.Cross;KeyPreview=true;DoubleBuffered=true;
  KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){DialogResult=DialogResult.Cancel;Close();}};
 }
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;start=e.Location;dragging=true;Capture=true;}
 void UpdateBox(Point p){p.X=Math.Max(0,Math.Min(ClientSize.Width,p.X));p.Y=Math.Max(0,Math.Min(ClientSize.Height,p.Y));box=Rectangle.FromLTRB(Math.Min(start.X,p.X),Math.Min(start.Y,p.Y),Math.Max(start.X,p.X),Math.Max(start.Y,p.Y));Invalidate();}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)UpdateBox(e.Location);}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!dragging||e.Button!=MouseButtons.Left)return;UpdateBox(e.Location);dragging=false;Capture=false;if(box.Width<100||box.Height<100)return;SelectedArea=new Rectangle(PointToScreen(box.Location),box.Size);DialogResult=DialogResult.OK;Close();}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(Color.FromArgb(130,172,255),3))e.Graphics.DrawRectangle(pen,box);}
}

internal sealed class MainWindow : Form {
 readonly Button select=Theme.Button("01   Selecionar área",false),capture=Theme.Button("02   Iniciar captura",true),save=Theme.Button("03   Salvar imagem",false);
 readonly Label status=new Label();readonly PictureBox preview=new PictureBox();Rectangle region;IntPtr target;Bitmap result;bool busy;
 public MainWindow() {
  Icon=Theme.AppIcon();Text="RolaPrint";Size=new Size(920,780);MinimumSize=new Size(760,600);StartPosition=FormStartPosition.CenterScreen;BackColor=Theme.Background;ForeColor=Theme.Text;Font=new Font("Segoe UI",10);
  var layout=new TableLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(32),ColumnCount=1,RowCount=5};
  layout.RowStyles.Add(new RowStyle(SizeType.Absolute,66));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,68));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,68));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,74));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(layout);
  layout.Controls.Add(new Label {Text="RolaPrint",Dock=DockStyle.Fill,ForeColor=Theme.Text,Font=new Font("Segoe UI",30,FontStyle.Bold)},0,0);
  layout.Controls.Add(new Label {Text="Uma captura. Todo o conteúdo.\nSelecione uma área, capture e salve.",Dock=DockStyle.Fill,ForeColor=Theme.Muted},0,1);
  var actions=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,Margin=Padding.Empty};for(int i=0;i<3;i++)actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));actions.Controls.Add(select,0,0);actions.Controls.Add(capture,1,0);actions.Controls.Add(save,2,0);layout.Controls.Add(actions,0,2);
  status.Text="Selecione a área do documento. Esc ou Parar finaliza a captura.";status.Dock=DockStyle.Fill;status.ForeColor=Theme.Muted;status.Padding=new Padding(0,12,0,0);layout.Controls.Add(status,0,3);
  preview.Dock=DockStyle.Fill;preview.SizeMode=PictureBoxSizeMode.Zoom;preview.BackColor=Theme.Surface;layout.Controls.Add(preview,0,4);
  preview.Paint+=(s,e)=>{using(var pen=new Pen(Color.FromArgb(42,42,47)))e.Graphics.DrawRectangle(pen,0,0,preview.Width-1,preview.Height-1);if(preview.Image==null){using(var heading=new Font("Segoe UI",15,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,"Tudo em uma imagem",heading,new Rectangle(0,preview.Height/2-35,preview.Width,36),Theme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);TextRenderer.DrawText(e.Graphics,"Sua captura aparece aqui depois de finalizar.",Font,new Rectangle(0,preview.Height/2+8,preview.Width,30),Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}};
  HandleCreated+=(s,e)=>Theme.DarkTitle(this);
  capture.Enabled=false;save.Enabled=false;select.Click+=SelectArea;capture.Click+=StartCapture;save.Click+=SaveImage;
  FormClosing+=(s,e)=>{if(busy){CaptureEngine.StopRequested=true;e.Cancel=true;}};
  FormClosed+=(s,e)=>{preview.Image=null;if(result!=null)result.Dispose();};
 }
 async void SelectArea(object sender,EventArgs e) {
  select.Enabled=false;Hide();
  try {using(var overlay=new SelectionWindow()){if(overlay.ShowDialog()==DialogResult.OK){region=overlay.SelectedArea;await Task.Delay(200);target=CaptureEngine.Target(region);capture.Enabled=true;status.Text=String.Format("Área: {0} × {1} pixels. Não mova a janela após selecionar.",region.Width,region.Height);}}}
  catch(Exception ex){MessageBox.Show(ex.Message,"RolaPrint");}
  finally{Show();Activate();select.Enabled=true;}
 }
 async void StartCapture(object sender,EventArgs e) {
  if(busy)return;busy=true;select.Enabled=capture.Enabled=save.Enabled=false;CaptureEngine.StopRequested=false;Point cursor=Cursor.Position;CaptureControls controls=null;
  try {
   Hide();CaptureEngine.Activate(target);await Task.Delay(700);
   if(!CaptureEngine.IsTarget(target,region))throw new InvalidOperationException("Selecione a área novamente: a janela original não está disponível.");
   Label notice;controls=CreateControls(out notice);
   var progress=new Progress<string>(message=>{if(!notice.IsDisposed)notice.Text=message;});
   Bitmap image=await Task.Run(()=>CaptureSession.Run(region,target,progress));
   preview.Image=null;if(result!=null)result.Dispose();result=image;preview.Image=image;
   status.Text=String.Format("Captura finalizada por você • {0} × {1} pixels. Confira a prévia e salve.",image.Width,image.Height);
  }catch(Exception ex){CaptureSession.Log(ex.ToString());status.Text="Não foi possível concluir a captura.";MessageBox.Show(ex.Message,"RolaPrint",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  finally{if(controls!=null)controls.Dispose();Cursor.Position=cursor;busy=false;select.Enabled=true;capture.Enabled=target!=IntPtr.Zero;save.Enabled=result!=null;Show();Activate();}
 }
 CaptureControls CreateControls(out Label notice) {
  var panel=new CaptureControls {Text="RolaPrint • captura",ClientSize=new Size(320,120),FormBorderStyle=FormBorderStyle.FixedToolWindow,TopMost=true,ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,BackColor=Theme.Background,ForeColor=Theme.Text,Font=Font};
  panel.Icon=Icon;
  panel.HandleCreated+=(s,e)=>Theme.DarkTitle(panel);
  notice=new Label {Text="Capturando. Esc ou Parar para finalizar.",Bounds=new Rectangle(12,8,296,52)};panel.Controls.Add(notice);
  var stop=Theme.Button("Parar e ver resultado",true);stop.Dock=DockStyle.None;stop.SetBounds(12,66,296,42);stop.Click+=(s,e)=>CaptureEngine.StopRequested=true;panel.Controls.Add(stop);
  panel.FormClosing+=(s,e)=>{CaptureEngine.StopRequested=true;e.Cancel=true;};
  foreach(var screen in Screen.AllScreens){Rectangle wa=screen.WorkingArea;foreach(var p in new[]{wa.Location,new Point(wa.Right-panel.Width,wa.Top),new Point(wa.Left,wa.Bottom-panel.Height),new Point(wa.Right-panel.Width,wa.Bottom-panel.Height)}){var bounds=new Rectangle(p,panel.Size);if(wa.Contains(bounds)&&!bounds.IntersectsWith(region)){panel.Location=p;panel.Show();return panel;}}}
  return panel;
 }
 void SaveImage(object sender,EventArgs e) {
  if(result==null)return;using(var dialog=new SaveFileDialog {Filter="Imagem PNG|*.png",FileName="RolaPrint-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png"}){
   if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{result.Save(dialog.FileName,ImageFormat.Png);status.Text="Imagem salva: "+dialog.FileName;}catch(Exception ex){MessageBox.Show(ex.Message,"Erro ao salvar");}
  }
 }
}

internal static class CaptureSession {
 internal static void Log(string message){string line=DateTime.Now.ToString("o")+" "+message+Environment.NewLine;try{var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RolaPrint");Directory.CreateDirectory(folder);File.AppendAllText(Path.Combine(folder,"RolaPrint.log"),line);}catch{}try{File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"RolaPrint.log"),line);}catch{}}
 public static Bitmap Run(Rectangle region,IntPtr target,IProgress<string> progress) {
  var parts=new List<Bitmap>();Bitmap previous=null;Point center=new Point(region.X+region.Width/2,region.Y+region.Height/2);int height=region.Height,tailStart=0,ticks=1,failedMatches=0,cautiousSteps=0;bool pending=false;
  CaptureEngine.MotionMask previousMotion=null;
  try {
   if((long)region.Width*region.Height>40000000)throw new InvalidOperationException("Selecione uma área menor.");
   Cursor.Position=center;parts.Add(CaptureEngine.Grab(region));previous=(Bitmap)parts[0].Clone();Log("Início C# diagnóstico vídeo v2 "+region);
   if(CaptureEngine.Wait(60)){using(var second=CaptureEngine.Grab(region))previousMotion=CaptureEngine.DetectMotion(previous,second);}
   while(!CaptureEngine.Cancelled()) {
    try {
     if(!CaptureEngine.IsTarget(target,region)){progress.Report("Aguardando a janela do documento. Esc ou Parar finaliza.");CaptureEngine.Wait(300);continue;}
     if(!pending){Cursor.Position=center;CaptureEngine.Scroll(ticks);pending=true;}
     if(!CaptureEngine.Wait(120))break;
     using(var next=CaptureEngine.Grab(region)) {
      if(!CaptureEngine.Wait(60))break;
      CaptureEngine.MotionMask motion;
      using(var stable=CaptureEngine.Grab(region)){motion=CaptureEngine.DetectMotion(next,stable);}
      // A large video must not block the whole viewport. Let static evidence
      // decide whether a scroll can be aligned, regardless of moving area size.
      if(!CaptureEngine.IsTarget(target,region))continue;
      var timer=System.Diagnostics.Stopwatch.StartNew();
      int shift=CaptureEngine.Match(previous,next,previousMotion,motion);timer.Stop();
      if(shift==-1){shift=CaptureEngine.Anchors(previous,next,previousMotion,motion);Log("Alinhamento por detalhes locais: "+shift);}
      if(shift==-1){shift=CaptureEngine.Consensus(previous,next,false);Log("Alinhamento por consenso: "+shift);}
      Log(String.Format("Alinhamento: {0}ms, resultado={1}, movimento={2:P0}, passos={3}",timer.ElapsedMilliseconds,shift,motion.Fraction,ticks));if(shift==-2)break;
      if(shift==0){pending=false;progress.Report("Sem mudança. Esc ou Parar para finalizar.");continue;}
      if(shift<0){
       failedMatches++;Log("Alinhamento incerto: passos="+ticks+", tentativa="+failedMatches);
       if(failedMatches==1){SaveDiagnostic(previous,next);}
       if(ticks>1 && failedMatches>=3){
        progress.Report("Ajustando a rolagem para este site...");
        if(!CaptureEngine.IsTarget(target,region))continue;
        Cursor.Position=center;CaptureEngine.Scroll(-ticks);
        bool restored=false;
        for(int attempt=0;attempt<5 && !CaptureEngine.Cancelled();attempt++){
         if(!CaptureEngine.Wait(180))break;
         if(!CaptureEngine.IsTarget(target,region))break;
         using(var back=CaptureEngine.Grab(region)){if(!CaptureEngine.Wait(60))break;using(var check=CaptureEngine.Grab(region)){var backMotion=CaptureEngine.DetectMotion(back,check);if(CaptureEngine.Match(previous,back,previousMotion,backMotion)==0 || CaptureEngine.Consensus(previous,back,true)==0){restored=true;break;}}}
        }
        if(CaptureEngine.Cancelled())break;
        if(restored){ticks=Math.Max(1,ticks/2);cautiousSteps=12;pending=false;failedMatches=0;Log("Posição anterior confirmada; reduzido para "+ticks+" passos");continue;}
        progress.Report("Não foi possível recuperar a posição. Pare e selecione uma área menor.");Log("Retorno não confirmado; coleta pausada");WaitForStop();break;
       }
       progress.Report("Poucos detalhes estáticos para alinhar. Inclua texto ao redor do vídeo; Esc ou Parar finaliza.");continue;
      }
      if((long)(height+shift)*region.Width>40000000 || height+shift>30000){progress.Report("Limite de tamanho. Esc ou Parar para salvar.");WaitForStop();break;}
      int seam=CaptureEngine.Seam(previous,next,shift,Math.Max(2,tailStart-shift+1),previousMotion,motion);int last=parts.Count-1;
      Bitmap finished=null,tail=null,newPrevious=null;
      try {finished=CaptureEngine.Crop(parts[last],0,shift+seam-tailStart);tail=CaptureEngine.Crop(next,seam,next.Height-seam);newPrevious=(Bitmap)next.Clone();}
      catch{if(finished!=null)finished.Dispose();if(tail!=null)tail.Dispose();if(newPrevious!=null)newPrevious.Dispose();throw;}
      parts[last].Dispose();parts[last]=finished;parts.Add(tail);previous.Dispose();previous=newPrevious;previousMotion=motion;tailStart=seam;height+=shift;pending=false;failedMatches=0;
      progress.Report("Capturando: "+height+" pixels. Esc ou Parar finaliza.");Log("Trecho "+shift+", altura "+height);
      // Calibrate using the observed movement, retaining ample overlap.
      if(cautiousSteps>0)cautiousSteps--;else ticks=Math.Max(1,Math.Min(ticks+1,Math.Min(8,(int)Math.Floor(region.Height*.32*ticks/shift))));
     }
    }catch(Exception ex){Log(ex.ToString());progress.Report("Erro na coleta. Esc ou Parar preserva os trechos capturados.");WaitForStop();break;}
   }
   var result=new Bitmap(region.Width,height,PixelFormat.Format24bppRgb);
   try {using(var g=Graphics.FromImage(result)){int y=0;foreach(var part in parts){g.DrawImageUnscaled(part,0,y);y+=part.Height;}}Log("Finalizado manualmente, altura "+height);return result;}catch{result.Dispose();throw;}
  }finally{if(previous!=null)previous.Dispose();foreach(var part in parts)part.Dispose();}
 }
 static void WaitForStop(){while(!CaptureEngine.Cancelled())CaptureEngine.Wait(100);}
 static void SaveDiagnostic(Bitmap previous,Bitmap current){try{string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"diagnostico");Directory.CreateDirectory(folder);previous.Save(Path.Combine(folder,"anterior.png"),ImageFormat.Png);current.Save(Path.Combine(folder,"atual.png"),ImageFormat.Png);}catch(Exception ex){Log("Diagnóstico: "+ex.Message);}}
}
