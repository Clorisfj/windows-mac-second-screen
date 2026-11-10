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
 sealed class MainForm:Form {
  readonly Coordinator flow;readonly NotifyIcon tray=new NotifyIcon();readonly Toggle extend=new Toggle(),desk=new Toggle();readonly Label device=new Label(),hint=new Label();readonly Row resolution,fps,bitrate;readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();bool working,polling,quitting;Image logo;Icon appIcon;
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
   extend.Toggled+=async(s,e)=>await Change(extend.Checked?"normal":"extend");desk.Toggled+=async(s,e)=>await Change(desk.Checked?"normal":"deskflow");
   tray.Icon=appIcon??SystemIcons.Application;tray.Text="CFlow";tray.Visible=true;tray.DoubleClick+=(s,e)=>Reveal();var menu=new ContextMenuStrip();menu.Items.Add("打开 CFlow",null,(s,e)=>Reveal());menu.Items.Add("设置",null,(s,e)=>{Reveal();OpenSettings();});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出 CFlow",null,async(s,e)=>await Quit());tray.ContextMenuStrip=menu;
   SystemEvents.UserPreferenceChanged+=SystemThemeChanged;FormClosing+=async(s,e)=>{if(quitting)return;if(e.CloseReason==CloseReason.WindowsShutDown){Admin.Shutdown();return;}e.Cancel=true;if(working)return;if(flow.Settings.CloseToTray)Hide();else await Quit();};
   FormClosed+=(s,e)=>{timer.Stop();tray.Visible=false;tray.Dispose();SystemEvents.UserPreferenceChanged-=SystemThemeChanged;if(logo!=null)logo.Dispose();if(appIcon!=null)appIcon.Dispose();};
   timer.Interval=4000;timer.Tick+=async(s,e)=>{if(working||polling)return;polling=true;extend.Enabled=desk.Enabled=resolution.Enabled=fps.Enabled=bitrate.Enabled=false;try{await Task.Run(()=>{lock(flow)flow.Poll();});UpdateState();}catch(Exception error){hint.Text=error.Message;}finally{polling=false;extend.Enabled=desk.Enabled=resolution.Enabled=fps.Enabled=bitrate.Enabled=!working;}};timer.Start();UpdateState();ApplyTheme();if(hidden)Shown+=(s,e)=>Hide();
  }
  static Label Label(string text,Rectangle rect,float size,bool bold,Color? color=null){return new Label{Text=text,Bounds=rect,Font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=color??Theme.Text,BackColor=Color.Transparent,AutoEllipsis=true,Tag=color.HasValue?"accent":null};}
  Card MakeCard(int x,int y,int w,int h){var card=new Card{Bounds=new Rectangle(x,y,w,h)};Controls.Add(card);return card;}
  static void Divider(Control parent,int y){parent.Controls.Add(new Panel{Bounds=new Rectangle(16,y,parent.Width-32,1),BackColor=Theme.Line,Tag="line"});}
  void SystemThemeChanged(object sender,UserPreferenceChangedEventArgs e){if(IsHandleCreated)BeginInvoke(new Action(()=>{Theme.Read();ApplyTheme();}));}
  void ApplyTheme(){BackColor=Theme.Background;PaintChildren(this);hint.ForeColor=Theme.Secondary;Invalidate(true);}
  static void PaintChildren(Control c){foreach(Control item in c.Controls){if(item.Tag as string=="line")item.BackColor=Theme.Line;else if(item is Label||item is Button)item.ForeColor=item.Tag as string=="accent"?Theme.Blue:Theme.Text;if(item is Card)item.Invalidate();PaintChildren(item);}}
  void Reveal(){Show();WindowState=FormWindowState.Normal;Activate();}
  void UpdateState(){extend.Checked=flow.Mode=="extend";desk.Checked=flow.Mode=="deskflow";extend.Invalidate();desk.Invalidate();resolution.Value=flow.Settings.Width+" × "+flow.Settings.Height;fps.Value=flow.Settings.Fps+" FPS";bitrate.Value=flow.Settings.Bitrate+" Mbps";resolution.Invalidate();fps.Invalidate();bitrate.Invalidate();device.Text=flow.Settings.Paired?flow.Settings.PeerName+"  ·  "+flow.PeerState:"未配对 Mac";device.ForeColor=flow.Online?Theme.Text:Theme.Secondary;hint.Text=working?"正在同步两端，请稍候…":flow.Settings.Paired?(flow.Mode=="normal"?"普通模式":"已启动对应程序；实际画面请在 Mac 确认"):"首次使用请点齿轮，连接 Mac CFlow。";}
  async Task Change(string mode){if(working)return;if(mode!="normal"&&!flow.Settings.Paired){OpenSettings();return;}working=true;extend.Enabled=desk.Enabled=false;resolution.Enabled=fps.Enabled=bitrate.Enabled=false;hint.Text="正在同步两端，请稍候…";
   try{await Task.Run(()=>{lock(flow)flow.Change(mode);});}
   catch(Exception e){MessageBox.Show(this,e.Message,"CFlow · 操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
   finally{working=false;extend.Enabled=desk.Enabled=true;resolution.Enabled=fps.Enabled=bitrate.Enabled=true;UpdateState();}
  }
  void ChooseResolution(){var menu=new ContextMenuStrip();foreach(var p in new[]{new[]{1920,1200},new[]{2560,1600},new[]{2880,1800}}){var pair=p;menu.Items.Add(p[0]+" × "+p[1],null,async(s,e)=>{flow.Settings.Width=pair[0];flow.Settings.Height=pair[1];flow.Settings.Save();UpdateState();if(flow.Mode=="extend")await Change("extend");});}menu.Show(resolution,new Point(120,40));}
  void ChooseNumber(Row target,int[] values,bool frame){var menu=new ContextMenuStrip();foreach(int number in values){int value=number;menu.Items.Add(number+(frame?" FPS":" Mbps"),null,async(s,e)=>{if(frame)flow.Settings.Fps=value;else flow.Settings.Bitrate=value;flow.Settings.Save();UpdateState();if(flow.Mode=="extend")await Change("extend");});}menu.Show(target,new Point(180,40));}
  void OpenSettings(){if(working||polling)return;timer.Stop();try{using(var dialog=new SettingsForm(flow,async()=>await Change("normal"))){dialog.ShowDialog(this);}flow.Settings.Save();UpdateState();ApplyTheme();}finally{timer.Start();}}
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
 sealed class SettingsForm:Form {
  readonly Coordinator flow;readonly Func<Task> stop;FlowLayoutPanel list=new FlowLayoutPanel();Card section;
  public SettingsForm(Coordinator coordinator,Func<Task> stopModes){flow=coordinator;stop=stopModes;Text="CFlow · 设置";ClientSize=new Size(470,580);StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",10);BackColor=Theme.Background;ForeColor=Theme.Text;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=MinimizeBox=false;
   list.Dock=DockStyle.Fill;list.Padding=new Padding(18);list.FlowDirection=FlowDirection.TopDown;list.WrapContents=false;list.AutoScroll=true;Controls.Add(list);
   StartGroup("通用");Add("外观","跟随系统",null);AddSwitch("开机启动 CFlow",Startup.Enabled,v=>Startup.Set(v));AddSwitch("关闭窗口时收起",flow.Settings.CloseToTray,v=>flow.Settings.CloseToTray=v);
   StartGroup("设备与连接");
   Add("双端控制配对",flow.Settings.Paired?flow.Settings.PeerName:"未配对",async()=>await Pair());
   Add("Moonlight 配对","检查 / 打开",()=>{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://localhost:47990"){UseShellExecute=true});MessageBox.Show(this,"CFlow 配对不替代 Sunshine / Moonlight 配对。请在 Mac Moonlight 中确认已配对此 Windows；主机管理页仅在 Sunshine 运行时可用。","串流配对");return Task.FromResult(0);});
   StartGroup("其他");AddSwitch("保留 Windows 本机声音",flow.Settings.AudioOnHost,v=>flow.Settings.AudioOnHost=v);
   Add("诊断与日志","本地查看",()=>{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe","\""+AppDomain.CurrentDomain.BaseDirectory+"\""){UseShellExecute=true});return Task.FromResult(0);});
   Add("恢复默认设置","保留配对与驱动",async()=>{if(MessageBox.Show(this,"关闭当前模式并重置 CFlow 偏好？\n保留配对、驱动与物理主屏设置。","恢复默认",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;await stop();if(flow.Mode!="normal")return;flow.Settings.ResetPreferences();Startup.Set(false);flow.Settings.Save();Close();});
   list.Controls.Add(new Label{Text="开机启动仅打开 CFlow，不自动开启模式。\n声音选项不会静音 Mac 的其他应用。",ForeColor=Theme.Secondary,AutoSize=false,Size=new Size(415,52),Padding=new Padding(8)});
   UserPreferenceChangedEventHandler changed=(s,e)=>{if(IsHandleCreated)BeginInvoke(new Action(()=>{Theme.Read();BackColor=Theme.Background;Recolor(list);}));};SystemEvents.UserPreferenceChanged+=changed;FormClosed+=(s,e)=>SystemEvents.UserPreferenceChanged-=changed;
  }
  void Recolor(Control root){foreach(Control c in root.Controls){if(c is Button){c.BackColor=Theme.Card;c.ForeColor=c.Text.StartsWith("恢复默认")?Color.FromArgb(235,70,65):Theme.Text;}else if(c is Label)c.ForeColor=c.Parent==list?Theme.Secondary:Theme.Text;else if(c is Panel&&!(c is FlowLayoutPanel))c.BackColor=c.Height==1?Theme.Line:Theme.Card;c.Invalidate();Recolor(c);}}
  void StartGroup(string name){list.Controls.Add(new Label{Text=name,ForeColor=Theme.Secondary,Width=418,Height=27,Padding=new Padding(3,5,0,0),Margin=new Padding(0,4,0,3)});section=new Card{Width=418,Height=0,Margin=new Padding(0,0,0,8)};list.Controls.Add(section);}
  void Place(Control c){int y=section.Height;c.Location=new Point(0,y+(y>0?1:0));if(y>0){section.Controls.Add(new Panel{Bounds=new Rectangle(12,y,394,1),BackColor=Theme.Line});}section.Height+=c.Height+(y>0?1:0);section.Controls.Add(c);if(section.Region!=null)section.Region.Dispose();using(var path=Theme.Rounded(new Rectangle(0,0,section.Width,section.Height),12))section.Region=new Region(path);}
  void Add(string name,string value,Func<Task> action){var button=new Button{Text=name+"     "+value+(action==null?"":"  ›"),Width=418,Height=48,TextAlign=ContentAlignment.MiddleLeft,FlatStyle=FlatStyle.Flat,BackColor=Theme.Card,ForeColor=name=="恢复默认设置"?Color.FromArgb(235,70,65):Theme.Text,Padding=new Padding(10,0,5,0),Margin=Padding.Empty};button.FlatAppearance.BorderSize=0;button.FlatAppearance.MouseOverBackColor=Theme.Dark?Color.FromArgb(54,54,59):Color.FromArgb(246,246,249);if(action!=null)button.Click+=async(s,e)=>{button.Enabled=false;try{await action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"CFlow",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{if(!button.IsDisposed)button.Enabled=true;}};Place(button);}
  void AddSwitch(string name,bool value,Action<bool> update){var card=new Panel{Width=418,Height=48,BackColor=Theme.Card};var label=new Label{Text=name,Location=new Point(12,14),AutoSize=true,ForeColor=Theme.Text};var toggle=new Toggle{Location=new Point(354,10),Checked=value,AccessibleName=name};toggle.Toggled+=(s,e)=>{bool next=!toggle.Checked;try{update(next);toggle.Checked=next;toggle.Invalidate();flow.Settings.Save();}catch(Exception ex){MessageBox.Show(ex.Message);}};card.Controls.Add(label);card.Controls.Add(toggle);Place(card);}
  async Task Pair(){
   using(var form=new Form{Text="连接 Mac CFlow",ClientSize=new Size(465,245),StartPosition=FormStartPosition.CenterParent,Font=Font,BackColor=Theme.Background,ForeColor=Theme.Text}){
    var label=new Label{Text="在 Mac 菜单栏 CFlow → 设置中复制配对码，粘贴到这里。\n配对码含设备密钥，请只交给自己的 Windows。",Bounds=new Rectangle(16,18,433,45)};var box=new TextBox{Multiline=true,Bounds=new Rectangle(16,74,433,100)};var save=new Button{Text="验证并连接",Bounds=new Rectangle(311,190,138,33),DialogResult=DialogResult.OK};var forget=new Button{Text="解除本机配对",Bounds=new Rectangle(16,190,150,33),DialogResult=DialogResult.Abort,Enabled=flow.Settings.Paired};form.Controls.AddRange(new Control[]{label,box,save,forget});form.AcceptButton=save;
    var decision=form.ShowDialog(this);
    if(decision==DialogResult.OK){var candidate=flow.Settings.Import(box.Text);await Task.Run(()=>Peer.Call(candidate,"status"));if(flow.Mode!="normal")await stop();if(flow.Mode!="normal")return;flow.Settings=candidate;candidate.Save();MessageBox.Show(this,"Mac 接收端已验证。Sunshine / Moonlight 的首次串流配对仍需在原应用完成。","CFlow");}
    else if(decision==DialogResult.Abort){await stop();if(flow.Mode!="normal")return;flow.Settings.PeerHost="";flow.Settings.PeerFingerprint="";flow.Settings.ProtectedToken="";flow.Settings.PeerName="Mac";flow.Online=false;flow.Settings.Save();MessageBox.Show(this,"本机已忘记该 Mac。若要让所有旧控制码失效，请在 Mac CFlow 设置中解除控制配对。","CFlow");}
   }
  }
 }
 static class Desktop {
  [STAThread] static int Main(string[] args){bool created;using(var mutex=new System.Threading.Mutex(true,"Local\\CFlow.UI",out created)){if(!created){MessageBox.Show("CFlow 已在托盘运行，双击托盘图标打开。");return 0;}try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.ThreadException+=(s,e)=>Failure(e.Exception);Application.Run(new MainForm(args.Contains("--tray")));return 0;}catch(Exception error){Failure(error);return 1;}}}
  static void Failure(Exception error){try{Directory.CreateDirectory(Settings.Folder);File.AppendAllText(Path.Combine(Settings.Folder,"errors.log"),DateTime.Now.ToString("s")+" "+error+Environment.NewLine);}catch{}MessageBox.Show(error.Message,"CFlow · 出错",MessageBoxButtons.OK,MessageBoxIcon.Error);}
 }
}
