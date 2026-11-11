using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CFlow {
 static class Theme {
  public static bool Dark;
  public static Color Background{get{return Dark?Color.FromArgb(24,24,26):Color.FromArgb(242,242,247);}}
  public static Color Card{get{return Dark?Color.FromArgb(39,39,42):Color.White;}}
  public static Color Text{get{return Dark?Color.FromArgb(242,242,245):Color.FromArgb(25,25,28);}}
  public static Color Secondary{get{return Dark?Color.FromArgb(163,163,172):Color.FromArgb(111,111,119);}}
  public static Color Line{get{return Dark?Color.FromArgb(65,65,70):Color.FromArgb(230,230,235);}}
  public static Color Blue=Color.FromArgb(22,119,255);
  public static void Read(){using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))Dark=k!=null&&Convert.ToInt32(k.GetValue("AppsUseLightTheme",1))==0;}
  public static GraphicsPath Rounded(Rectangle r,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 }
 sealed class Toggle:Control {
  public bool Checked;public event EventHandler Toggled;
  public Toggle(){Size=new Size(48,28);Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;TabStop=true;AccessibleRole=AccessibleRole.CheckButton;}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var rect=new Rectangle(1,1,46,26);using(var path=Theme.Rounded(rect,13))using(var b=new SolidBrush(Checked?Color.FromArgb(48,209,88):Theme.Dark?Color.FromArgb(85,85,91):Color.FromArgb(209,209,214)))e.Graphics.FillPath(b,path);using(var b=new SolidBrush(Color.White))e.Graphics.FillEllipse(b,Checked?23:3,3,22,22);if(!Enabled)using(var b=new SolidBrush(Color.FromArgb(80,Theme.Background)))e.Graphics.FillRectangle(b,ClientRectangle);}
  protected override void OnClick(EventArgs e){base.OnClick(e);if(Enabled&&Toggled!=null)Toggled(this,EventArgs.Empty);}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space){OnClick(EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
 }
 sealed class Card:Panel {
  public Card(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Color.Transparent;}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Theme.Rounded(new Rectangle(0,0,Width-1,Height-1),13))using(var b=new SolidBrush(Theme.Card))e.Graphics.FillPath(b,p);base.OnPaint(e);}
 }
 sealed class ThemeButton:Button {
  protected override void OnPaint(PaintEventArgs e){if(Enabled){base.OnPaint(e);return;}e.Graphics.Clear(Theme.Card);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Theme.Secondary,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}
 }
 sealed class MainForm:Form {
  readonly Coordinator flow;readonly NotifyIcon tray=new NotifyIcon();readonly Toggle extend=new Toggle(),desk=new Toggle();readonly Label device=new Label(),hint=new Label();readonly Row resolution,fps,bitrate;readonly Panel mainPage=new Panel{Dock=DockStyle.Fill};SettingsPage settingsPage;readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();bool working,polling,quitting;Image logo;Icon appIcon;
  public MainForm(bool hidden){
   flow=new Coordinator(Settings.Load());Text="CFlow";ClientSize=new Size(438,500);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;Theme.Read();
   string icon=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CFlow.ico"),png=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CFlow.png");if(File.Exists(icon)){appIcon=new Icon(icon);Icon=appIcon;}if(File.Exists(png))logo=Image.FromFile(png);
   var brand=new PictureBox{Location=new Point(22,19),Size=new Size(40,40),Image=logo,SizeMode=PictureBoxSizeMode.Zoom};Controls.Add(brand);Controls.Add(Label("CFlow",new Rectangle(72,22,235,36),22,true));
   var gear=new Button{Text="⚙",Font=new Font("Segoe UI Symbol",19),Location=new Point(371,22),Size=new Size(43,36),FlatStyle=FlatStyle.Flat,TabIndex=0};gear.FlatAppearance.BorderSize=0;gear.Click+=(s,e)=>OpenSettings();Controls.Add(gear);
   var connection=MakeCard(22,81,394,53);connection.Controls.Add(new DeviceGlyph{Bounds=new Rectangle(15,13,82,27)});device.SetBounds(110,16,270,23);connection.Controls.Add(device);
   var modes=MakeCard(22,153,394,108);modes.Controls.Add(Label("▣",new Rectangle(16,18,28,25),16,false,Theme.Blue));modes.Controls.Add(Label("扩展屏",new Rectangle(55,18,235,26),11,false));extend.Location=new Point(327,15);extend.AccessibleName="扩展屏开关";modes.Controls.Add(extend);
   modes.Controls.Add(Label("↖",new Rectangle(16,70,28,25),17,false,Theme.Blue));modes.Controls.Add(Label("共用鼠标",new Rectangle(55,72,235,26),11,false));desk.Location=new Point(327,68);desk.AccessibleName="共用鼠标开关";modes.Controls.Add(desk);Divider(modes,54);
   var parameters=MakeCard(22,280,394,153);resolution=new Row("分辨率","",0);fps=new Row("帧率","",1);bitrate=new Row("码率","",2);foreach(var row in new[]{resolution,fps,bitrate})parameters.Controls.Add(row);resolution.Clicked+=(s,e)=>ChooseResolution();fps.Clicked+=(s,e)=>ChooseNumber(fps,new[]{30,60},true);bitrate.Clicked+=(s,e)=>ChooseNumber(bitrate,new[]{20,30,40,60,80},false);
   hint.SetBounds(24,449,390,39);hint.Font=new Font(Font.FontFamily,9);Controls.Add(hint);
   foreach(Control child in Controls.Cast<Control>().ToArray())mainPage.Controls.Add(child);Controls.Add(mainPage);
   extend.Toggled+=async(s,e)=>await Change(extend.Checked?"normal":"extend");desk.Toggled+=async(s,e)=>await Change(desk.Checked?"normal":"deskflow");
   tray.Icon=appIcon??SystemIcons.Application;tray.Text="CFlow";tray.Visible=true;tray.DoubleClick+=(s,e)=>Reveal();var menu=new ContextMenuStrip();menu.Items.Add("打开 CFlow",null,(s,e)=>Reveal());menu.Items.Add("设置",null,(s,e)=>{Reveal();OpenSettings();});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出 CFlow",null,async(s,e)=>await Quit());tray.ContextMenuStrip=menu;
   SystemEvents.UserPreferenceChanged+=SystemThemeChanged;FormClosing+=async(s,e)=>{if(quitting)return;if(e.CloseReason==CloseReason.WindowsShutDown){Admin.Shutdown();return;}e.Cancel=true;if(working)return;if(flow.Settings.CloseToTray)Hide();else await Quit();};
   FormClosed+=(s,e)=>{timer.Stop();tray.Visible=false;tray.Dispose();SystemEvents.UserPreferenceChanged-=SystemThemeChanged;if(logo!=null)logo.Dispose();if(appIcon!=null)appIcon.Dispose();};
   timer.Interval=4000;timer.Tick+=async(s,e)=>{if(working||polling||(settingsPage!=null&&settingsPage.IsBusy))return;polling=true;extend.Enabled=desk.Enabled=resolution.Enabled=fps.Enabled=bitrate.Enabled=false;try{await Task.Run(()=>{lock(flow)flow.Poll();});UpdateState();}catch(Exception error){hint.Text=error.Message;}finally{polling=false;extend.Enabled=desk.Enabled=resolution.Enabled=fps.Enabled=bitrate.Enabled=!working;}};timer.Start();UpdateState();ApplyTheme();if(hidden)Shown+=(s,e)=>Hide();
  }
  static Label Label(string text,Rectangle rect,float size,bool bold,Color? color=null){return new Label{Text=text,Bounds=rect,Font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=color??Theme.Text,BackColor=Color.Transparent,AutoEllipsis=true,Tag=color.HasValue?"accent":null};}
  Card MakeCard(int x,int y,int w,int h){var card=new Card{Bounds=new Rectangle(x,y,w,h)};Controls.Add(card);return card;}
  static void Divider(Control parent,int y){parent.Controls.Add(new Panel{Bounds=new Rectangle(16,y,parent.Width-32,1),BackColor=Theme.Line,Tag="line"});}
  void SystemThemeChanged(object sender,UserPreferenceChangedEventArgs e){if(IsHandleCreated)BeginInvoke(new Action(()=>{Theme.Read();ApplyTheme();}));}
  void ApplyTheme(){BackColor=Theme.Background;PaintChildren(this);hint.ForeColor=Theme.Secondary;Invalidate(true);}
  internal static void PaintChildren(Control c){foreach(Control item in c.Controls){if(item is SettingsPage)item.BackColor=Theme.Background;if(item is Panel&&item.Parent is Card&&item.Tag as string!="line")item.BackColor=Theme.Card;if(item.Tag as string=="line")item.BackColor=Theme.Line;else if(item is Label||item is Button)item.ForeColor=item.Tag as string=="accent"?Theme.Blue:item.Tag as string=="secondary"?Theme.Secondary:item.Tag as string=="danger"?Color.FromArgb(235,90,85):Theme.Text;if(item is Button){item.BackColor=Theme.Card;((Button)item).UseVisualStyleBackColor=false;}if(item is TextBox){item.BackColor=Theme.Card;item.ForeColor=Theme.Text;}if(item is Card)item.Invalidate();PaintChildren(item);}}
  void Reveal(){Show();WindowState=FormWindowState.Normal;Activate();}
  void UpdateState(){
   extend.Checked=flow.Mode=="extend";desk.Checked=flow.Mode=="deskflow";
   extend.Invalidate();desk.Invalidate();
   resolution.Value=flow.Settings.Width+" × "+flow.Settings.Height;
   fps.Value=flow.Settings.Fps+" FPS";bitrate.Value=flow.Settings.Bitrate+" Mbps";
   resolution.Invalidate();fps.Invalidate();bitrate.Invalidate();
   device.AutoEllipsis=true;
   device.Text=flow.Settings.Paired?flow.Settings.PeerName+" · "+(flow.Online?"已连接":"未连接"):"未配对 Mac";
   device.ForeColor=flow.Online?Theme.Text:Theme.Secondary;
   if(working)hint.Text="正在同步两端，请稍候…";
   else if(!flow.Settings.Paired)hint.Text="首次使用请点齿轮，按 IP 连接 Mac CFlow。";
   else if(!flow.Online||flow.PeerState.StartsWith("切换失败"))hint.Text=flow.PeerState;
   else if(flow.Mode=="deskflow")hint.Text="Windows："+LocalNetwork.Addresses(flow.Settings.PeerHost).Replace(Environment.NewLine," / ")+" · "+ScreenDesk.Engine.DeskPort+"\n服务端已监听；跨屏控制请在 Mac 确认";
   else hint.Text=flow.Mode=="normal"?"普通模式":"已启动对应程序；实际画面请在 Mac 确认";
  }
  async Task Change(string mode){if(working)return;while(polling)await Task.Delay(100);if(working)return;if(mode!="normal"&&!flow.Settings.Paired){OpenSettings();if(settingsPage!=null)settingsPage.PairPage("先完成 CFlow 双端配对，才能同时启动 Windows 和 Mac。");return;}working=true;extend.Enabled=desk.Enabled=false;resolution.Enabled=fps.Enabled=bitrate.Enabled=false;hint.Text="正在同步两端，请稍候…";
   try{await Task.Run(()=>{lock(flow)flow.Change(mode);});}
   catch(Exception e){MessageBox.Show(this,e.Message,"CFlow · 操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
   finally{working=false;extend.Enabled=desk.Enabled=true;resolution.Enabled=fps.Enabled=bitrate.Enabled=true;UpdateState();}
  }
  void ChooseResolution(){var menu=new ContextMenuStrip();foreach(var p in new[]{new[]{1920,1200},new[]{2560,1600},new[]{2880,1800}}){var pair=p;menu.Items.Add(p[0]+" × "+p[1],null,async(s,e)=>{flow.Settings.Width=pair[0];flow.Settings.Height=pair[1];flow.Settings.Save();UpdateState();if(flow.Mode=="extend")await Change("extend");});}menu.Show(resolution,new Point(120,40));}
  void ChooseNumber(Row target,int[] values,bool frame){var menu=new ContextMenuStrip();foreach(int number in values){int value=number;menu.Items.Add(number+(frame?" FPS":" Mbps"),null,async(s,e)=>{if(frame)flow.Settings.Fps=value;else flow.Settings.Bitrate=value;flow.Settings.Save();UpdateState();if(flow.Mode=="extend")await Change("extend");});}menu.Show(target,new Point(180,40));}
  async void OpenSettings(){if(working)return;while(polling)await Task.Delay(100);if(working)return;if(settingsPage!=null)return;settingsPage=new SettingsPage(flow,async()=>await Change("normal"),CloseSettings){Dock=DockStyle.Fill};mainPage.Hide();Controls.Add(settingsPage);settingsPage.BringToFront();ApplyTheme();}
  void CloseSettings(){if(working)return;flow.Settings.Save();if(settingsPage!=null){Controls.Remove(settingsPage);settingsPage.Dispose();settingsPage=null;}mainPage.Show();UpdateState();ApplyTheme();timer.Start();}
  async Task Quit(){if(working)return;await Change("normal");if(flow.Mode!="normal")return;Admin.Shutdown();quitting=true;Close();}
 }
 sealed class DeviceGlyph:Control {
  public DeviceGlyph(){SetStyle(ControlStyles.SupportsTransparentBackColor|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint,true);BackColor=Color.Transparent;}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=new Pen(Theme.Blue,1.6f)){e.Graphics.DrawRectangle(p,2,3,25,16);e.Graphics.DrawLine(p,14,19,14,23);e.Graphics.DrawLine(p,8,23,20,23);e.Graphics.DrawLine(p,34,12,45,12);e.Graphics.DrawRectangle(p,54,5,22,14);e.Graphics.DrawLine(p,51,21,79,21);}}
 }
 sealed class Row:Control {
  public string Value;readonly string title;public event EventHandler Clicked;
  public Row(string text,string value,int index){title=text;Value=value;Bounds=new Rectangle(0,index*51,394,51);Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;TabStop=true;AccessibleName=text;}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var blue=new SolidBrush(Theme.Blue))using(var normal=new SolidBrush(Theme.Text))using(var gray=new SolidBrush(Theme.Secondary))using(var font=new Font("Microsoft YaHei UI",10.5f)){e.Graphics.DrawString(title=="分辨率"?"⌗":title=="帧率"?"∿":"◴",new Font("Segoe UI Symbol",17),blue,16,10);e.Graphics.DrawString(title,font,normal,55,15);var format=new StringFormat{Alignment=StringAlignment.Far};e.Graphics.DrawString(Value,font,normal,new RectangleF(160,15,190,24),format);e.Graphics.DrawString("›",font,gray,365,15);}if(Top>0)using(var pen=new Pen(Theme.Line))e.Graphics.DrawLine(pen,16,0,Width-16,0);}
  protected override void OnClick(EventArgs e){base.OnClick(e);if(Enabled&&Clicked!=null)Clicked(this,EventArgs.Empty);}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){OnClick(EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
 }
 sealed class SettingsPage:UserControl {
  readonly Coordinator flow;readonly Func<Task> stop;readonly Action back;readonly FlowLayoutPanel list=new FlowLayoutPanel();Card section;bool busy;System.Threading.CancellationTokenSource pairingCancellation;
  public bool IsBusy{get{return busy;}}
  protected override void Dispose(bool disposing){if(disposing&&pairingCancellation!=null){pairingCancellation.Cancel();pairingCancellation.Dispose();}base.Dispose(disposing);}
  public SettingsPage(Coordinator coordinator,Func<Task> stopModes,Action goBack){flow=coordinator;stop=stopModes;back=goBack;Font=new Font("Microsoft YaHei UI",10);list.Dock=DockStyle.Fill;list.Padding=new Padding(22,12,22,12);list.FlowDirection=FlowDirection.TopDown;list.WrapContents=false;list.AutoScroll=true;Controls.Add(list);Home();}
  void Clear(string title,Action previous){foreach(Control c in list.Controls.Cast<Control>().ToArray())c.Dispose();list.Controls.Clear();list.AutoScrollPosition=Point.Empty;BackColor=Theme.Background;var header=new Panel{Width=378,Height=52};var button=Button("‹ 返回",new Rectangle(0,4,88,36));button.Click+=(s,e)=>{if(!busy)previous();};header.Controls.Add(button);header.Controls.Add(new Label{Text=title,Bounds=new Rectangle(98,8,280,34),Font=new Font(Font.FontFamily,17,FontStyle.Bold),ForeColor=Theme.Text});list.Controls.Add(header);}
  Button Button(string title,Rectangle bounds){var b=new ThemeButton{Text=title,Bounds=bounds,FlatStyle=FlatStyle.Flat,BackColor=Theme.Card,ForeColor=Theme.Text,UseVisualStyleBackColor=false};b.FlatAppearance.BorderSize=0;return b;}
  void Note(string text,int height){list.Controls.Add(new Label{Text=text,Width=378,Height=height,ForeColor=Theme.Secondary,Tag="secondary",Padding=new Padding(4,6,4,0),Margin=new Padding(0,4,0,8)});}
  void Home(){
   Clear("设置",back);StartGroup("通用");AddSwitch("开机启动 CFlow",Startup.Enabled,v=>Startup.Set(v));AddSwitch("关闭窗口时收起",flow.Settings.CloseToTray,v=>flow.Settings.CloseToTray=v);
   StartGroup("设备与连接");Add("双端控制配对",flow.Settings.Paired?flow.Settings.PeerName:"未配对",()=>{PairPage("");return Task.FromResult(0);});
   Add("Moonlight 串流配对","查看步骤",()=>{MoonlightPage();return Task.FromResult(0);});Add("Deskflow 连接","地址 / 状态",()=>{DeskflowPage();return Task.FromResult(0);});
   StartGroup("其他");AddSwitch("保留 Windows 本机声音",flow.Settings.AudioOnHost,v=>flow.Settings.AudioOnHost=v);
   Add("诊断与日志","本地查看",()=>{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe","\""+AppDomain.CurrentDomain.BaseDirectory+"\""){UseShellExecute=true});return Task.FromResult(0);});
   Add("恢复默认设置","保留配对与驱动",async()=>{if(MessageBox.Show(this,"关闭当前模式并重置 CFlow 偏好？\n保留配对、驱动与物理主屏设置。","恢复默认",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;await stop();if(flow.Mode!="normal")return;flow.Settings.ResetPreferences();Startup.Set(false);flow.Settings.Save();Home();});
   Note("开机启动仅打开 CFlow，不自动开启模式。\n声音选项不会静音 Mac 的其他应用。",55);
  }
  void StartGroup(string name){list.Controls.Add(new Label{Text=name,ForeColor=Theme.Secondary,Tag="secondary",Width=378,Height=27,Padding=new Padding(3,5,0,0),Margin=new Padding(0,4,0,3)});section=new Card{Width=378,Height=0,Margin=new Padding(0,0,0,8)};list.Controls.Add(section);}
  void Place(Control c){int y=section.Height;c.Location=new Point(0,y+(y>0?1:0));if(y>0)section.Controls.Add(new Panel{Bounds=new Rectangle(12,y,354,1),BackColor=Theme.Line,Tag="line"});section.Height+=c.Height+(y>0?1:0);section.Controls.Add(c);}
  void Add(string name,string value,Func<Task> action){var b=Button(name+"    "+value+"  ›",new Rectangle(0,0,378,48));b.TextAlign=ContentAlignment.MiddleLeft;b.Padding=new Padding(10,0,0,0);if(name=="恢复默认设置"){b.Tag="danger";b.ForeColor=Color.FromArgb(235,90,85);}b.Click+=async(s,e)=>{if(busy)return;busy=true;b.Enabled=false;try{await action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"CFlow",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{busy=false;if(!b.IsDisposed)b.Enabled=true;}};Place(b);}
  void AddSwitch(string name,bool value,Action<bool> update){var card=new Panel{Width=378,Height=48,BackColor=Theme.Card};card.Controls.Add(new Label{Text=name,Location=new Point(12,14),AutoSize=true,ForeColor=Theme.Text});var toggle=new Toggle{Location=new Point(314,10),Checked=value,AccessibleName=name};toggle.Toggled+=(s,e)=>{try{bool next=!toggle.Checked;update(next);toggle.Checked=next;toggle.Invalidate();flow.Settings.Save();}catch(Exception ex){MessageBox.Show(this,ex.Message,"CFlow");}};card.Controls.Add(toggle);Place(card);}
  void Address(){string addresses=LocalNetwork.Addresses(flow.Settings.PeerHost);var box=new TextBox{Text=addresses,ReadOnly=true,Multiline=true,Width=378,Height=60,BackColor=Theme.Card,ForeColor=Theme.Text,BorderStyle=BorderStyle.FixedSingle};list.Controls.Add(box);}
  void MoonlightPage(){Clear("串流配对",Home);Note("原来已配对且能串流时，无需重新配对。",45);Address();Note("首次配对：\n1. 在 Mac 打开 Moonlight，添加上方 Windows 地址。\n2. Moonlight 显示 PIN 后，在 Windows Sunshine 管理页确认。\n\n这是串流应用配对，与 CFlow 双端控制配对独立。",160);
   bool running=ScreenDesk.Engine.ServiceState("SunshineService")=="Running";
   StartGroup("Sunshine");if(running)Add("主机管理页","打开",()=>{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://localhost:47990"){UseShellExecute=true});return Task.FromResult(0);});
   Note(running?"Sunshine 正在运行，可以打开本机管理页。":"Sunshine 当前未运行，因此暂不打开网页。\n已有串流配对请直接完成 CFlow 配对，再开启扩展屏。首次串流配对请先手动启动 Sunshine。",105);
  }
  void DeskflowPage(){Clear("Deskflow 连接",Home);Note("Windows 主机地址（可选中复制）",40);Address();Note("端口："+ScreenDesk.Engine.DeskPort+"\nDeskflow 名称："+ScreenDesk.Engine.DeskName+"\nWindows 服务端："+(ScreenDesk.Engine.DeskListening()?"正在监听":"未监听"),85);Note("CFlow 双端配对后，返回首页开启“共用鼠标”，会先启动 Windows 服务端，再通知 Mac 启动客户端。\n\nDeskflow 沿用原有配对、证书与屏幕布局。Mac 原客户端主机地址应与上方一致。不能跨屏时也请检查 Scroll Lock 是否开启。",170);StartGroup("连接");Add("刷新状态","重新检查",()=>{DeskflowPage();return Task.FromResult(0);});}
  public void PairPage(string reason){
   Clear("连接 Mac",Home);
   Note(reason.Length>0?reason:"在 Mac CFlow 设置中开启“允许配对”，然后输入 Mac 的 IP。",65);
   Note("Windows IP  "+LocalNetwork.Addresses(flow.Settings.PeerHost).Replace(Environment.NewLine," / "),40);Note("Mac IP",28);
   var host=new TextBox{Text=flow.Settings.PeerHost,Width=378,Height=32,BackColor=Theme.Card,ForeColor=Theme.Text};list.Controls.Add(host);
   var result=new Label{Width=378,Height=90,ForeColor=Theme.Secondary,Tag="secondary",Text=flow.Settings.Paired?"已保存配对，可重新连接同一台 Mac。":"连接后核对两端相同的 6 位数字，在 Mac 点允许。",Padding=new Padding(4,8,4,0)};
   var actions=new Panel{Width=378,Height=48};var forget=Button("解除本机配对",new Rectangle(0,4,160,36));forget.Enabled=flow.Settings.Paired;var connect=Button("搜索并配对",new Rectangle(214,4,164,36));actions.Controls.Add(forget);actions.Controls.Add(connect);list.Controls.Add(actions);list.Controls.Add(result);
   connect.Click+=async(s,e)=>{
    if(busy)return;busy=true;connect.Enabled=host.Enabled=false;forget.Enabled=true;forget.Text="取消配对";
    pairingCancellation=new System.Threading.CancellationTokenSource();var cancellation=pairingCancellation.Token;
    try{
     result.Text="正在连接 Mac…";string ip=host.Text.Trim();
     var session=await Task.Run(()=>Pairing.Begin(ip));cancellation.ThrowIfCancellationRequested();
     result.Text="验证码  "+session.Code+"\n请在 Mac 核对相同数字并点允许。";
     Settings candidate=null;var deadline=DateTime.UtcNow.AddSeconds(115);
     while(candidate==null&&DateTime.UtcNow<deadline){
      await Task.Delay(1000,cancellation);candidate=await Task.Run(()=>Pairing.Complete(flow.Settings,session));cancellation.ThrowIfCancellationRequested();
     }
     if(candidate==null)throw new Exception("配对超时，请在 Mac 重新开启允许配对。");
     await Task.Run(()=>Peer.Call(candidate,"status"));cancellation.ThrowIfCancellationRequested();
     if(flow.Mode!="normal")await stop();if(flow.Mode!="normal")return;
     flow.Settings=candidate;candidate.Save();flow.Online=true;flow.PeerState="已连接";result.Text="已连接 "+candidate.PeerName+"。返回首页即可开启模式。";
    }catch(OperationCanceledException){if(!IsDisposed)result.Text="配对已取消。";}
    catch(Exception error){if(!IsDisposed)result.Text=error.GetBaseException().Message;}
    finally{busy=false;if(!IsDisposed){connect.Enabled=host.Enabled=true;forget.Text="解除本机配对";forget.Enabled=flow.Settings.Paired;}}
   };
   forget.Click+=async(s,e)=>{
    if(busy){if(pairingCancellation!=null)pairingCancellation.Cancel();return;}
    busy=true;connect.Enabled=forget.Enabled=false;
    try{await stop();if(flow.Mode!="normal")return;flow.Settings.PeerHost="";flow.Settings.PeerFingerprint="";flow.Settings.ProtectedToken="";flow.Settings.PeerName="Mac";flow.Online=false;flow.Settings.Save();result.Text="本机已解除配对。";}
    catch(Exception error){result.Text=error.GetBaseException().Message;}
    finally{busy=false;connect.Enabled=true;forget.Enabled=flow.Settings.Paired;}
   };
  }

 }
 static class Desktop {
  [STAThread] static int Main(string[] args){bool created;using(var mutex=new System.Threading.Mutex(true,"Local\\CFlow.UI",out created)){if(!created){MessageBox.Show("CFlow 已在托盘运行，双击托盘图标打开。");return 0;}try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.ThreadException+=(s,e)=>Failure(e.Exception);Application.Run(new MainForm(args.Contains("--tray")));return 0;}catch(Exception error){Failure(error);return 1;}}}
  static void Failure(Exception error){try{Directory.CreateDirectory(Settings.Folder);File.AppendAllText(Path.Combine(Settings.Folder,"errors.log"),DateTime.Now.ToString("s")+" "+error+Environment.NewLine);}catch{}MessageBox.Show(error.Message,"CFlow · 出错",MessageBoxButtons.OK,MessageBoxIcon.Error);}
 }
}
