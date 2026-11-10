using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace ScreenDesk {
    public sealed class Preset {
        public int Width = 2880, Height = 1800, Hz = 60, Bitrate = 40;
        public static Preset Load() {
            try { var x = XDocument.Load(Path.Combine(Engine.Home, "preset.xml")).Root;
                return new Preset { Width=(int)x.Element("width"), Height=(int)x.Element("height"), Hz=(int)x.Element("hz"), Bitrate=(int)x.Element("bitrate") }; }
            catch { return new Preset(); }
        }
        public void Validate() {
            if (!((Width==1920 && Height==1200)||(Width==2560 && Height==1600)||(Width==2880 && Height==1800)) || (Hz!=30 && Hz!=60) || Bitrate<10 || Bitrate>80)
                throw new Exception("预设参数无效，请重新选择。");
        }
        public void Save() { Validate(); new XDocument(new XElement("preset", new XElement("width",Width),new XElement("height",Height),new XElement("hz",Hz),new XElement("bitrate",Bitrate))).Save(Path.Combine(Engine.Home,"preset.xml")); }
        public string MacText() { return "Moonlight："+Width+"×"+Height+" / "+Hz+"FPS / HEVC / "+Bitrate+"Mbps，Mac 全屏；在 Mac 中手动设置后重新连接 Desktop。"; }
    }
    public static class Engine {
        public static readonly string Home = AppDomain.CurrentDomain.BaseDirectory;
        public const string Sunshine = @"D:\moonlight";
        public const string Deskflow = @"D:\deskflow";
        public const string VddId = @"@ROOT\DISPLAY\0001";
        static string Devcon { get { return Path.Combine(Sunshine,@"VDD\Dependencies\devcon.exe"); } }
        static string LogPath { get { return Path.Combine(Home,"control.log"); } }
        public static void Log(string s) { lock(typeof(Engine)) { File.AppendAllText(LogPath,DateTime.Now.ToString("s")+" "+s+Environment.NewLine,Encoding.UTF8); } }
        static void Report(Action<string> progress,string s) { Log(s); if(progress!=null)progress(s); }
        public static int Run(string exe,string args) {
            var info=new ProcessStartInfo(exe,args) { UseShellExecute=false, CreateNoWindow=true, WindowStyle=ProcessWindowStyle.Hidden, RedirectStandardOutput=true, RedirectStandardError=true };
            using(var p=new Process { StartInfo=info }) {
                p.Start(); var output=p.StandardOutput.ReadToEndAsync(); var error=p.StandardError.ReadToEndAsync();
                if(!p.WaitForExit(30000)) { try{p.Kill();}catch{} throw new Exception("操作超时："+Path.GetFileName(exe)); }
                Task.WaitAll(output,error);
                Log(Path.GetFileName(exe)+" "+args+" => "+p.ExitCode+" "+output.Result.Trim()+" "+error.Result.Trim()); return p.ExitCode;
            }
        }
        static void DeviceCommand(string action) {
            // Confirm the exact installed root device, never operate on a GPU by a wildcard.
            using(var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\ROOT\DISPLAY\0001")) {
                if(key==null || !((key.GetValue("HardwareID") as string[])??new string[0]).Any(s=>s.Equals(@"Root\MttVDD",StringComparison.OrdinalIgnoreCase)))
                    throw new Exception("VDD 设备身份与安装记录不匹配，已停止操作。");
            }
            int code=Run(Devcon,action+" \""+VddId+"\"");
            if(code==1)throw new Exception("驱动要求重启电脑。没有自动重启，请保存工作后手动重启。");
            if(code!=0)throw new Exception("VDD "+action+"失败，查看 control.log。");
        }
        public static string ServiceState(string name) { using(var s=new ServiceController(name)) return s.Status.ToString(); }
        static void StopService(string name) { using(var s=new ServiceController(name)) {
            if(s.Status==ServiceControllerStatus.Stopped)return;
            if(s.Status!=ServiceControllerStatus.StopPending)s.Stop();
            // Sunshine can still be initializing its tray when stop is requested.
            // The VDD has already been disconnected, so wait for orderly service exit.
            s.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(40));
        } }
        static void StartService(string name) { using(var s=new ServiceController(name)) { if(s.Status==ServiceControllerStatus.Running)return; s.Start(); s.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(20)); } }
        static bool IsOwned(Process p,string root) { try { return p.MainModule.FileName.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase); } catch { return false; } }
        static void StopApps(string root,params string[] names) {
            foreach(var name in names)foreach(var p in Process.GetProcessesByName(name))using(p) {
                if(!IsOwned(p,root))throw new Exception("同名程序路径不匹配，未停止："+name);
                if(p.MainWindowHandle!=IntPtr.Zero){p.CloseMainWindow(); if(p.WaitForExit(1000))continue;}
                p.Kill(); if(!p.WaitForExit(5000))throw new Exception("未能停止："+name);
            }
        }
        static DisplaySetup.Device[] Active() { return DisplaySetup.Devices("").Where(d=>(d.flags&1)!=0).ToArray(); }
        static DisplaySetup.Device Primary() { return Active().Single(d=>(d.flags&4)!=0); }
        static DisplaySetup.Device[] Virtual() { return Active().Where(d=>d.description=="Virtual Display Driver").ToArray(); }
        static bool Same(DisplaySetup.Mode a, DisplaySetup.Mode b) { return a.width==b.width&&a.height==b.height&&a.hz==b.hz&&a.x==b.x&&a.y==b.y&&a.bits==b.bits; }
        static void CheckPrimary(string name,DisplaySetup.Mode before) {
            if(Primary().name!=name || !Same(before,DisplaySetup.GetMode(name,-1)))throw new Exception("主屏状态发生变化，请检查显示设置。未尝试修改主屏。");
        }
        public static void Backup(string path) {
            if(!File.Exists(path))return; var dir=Path.Combine(Home,"backups");Directory.CreateDirectory(dir);
            File.Copy(path,Path.Combine(dir,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Path.GetFileName(path)),false);
        }
        static void EnsureMode(Preset p) {
            string path=@"C:\VirtualDisplayDriver\vdd_settings.xml";var x=XDocument.Load(path);var resolutions=x.Root.Element("resolutions");
            if(resolutions==null)throw new Exception("VDD 配置格式不匹配。");
            if(!resolutions.Elements("resolution").Any(r=>(string)r.Element("width")==p.Width.ToString()&&(string)r.Element("height")==p.Height.ToString()&&r.Elements("refresh_rate").Any(h=>(string)h==p.Hz.ToString()))) {
                Backup(path);resolutions.Add(new XElement("resolution",new XElement("width",p.Width),new XElement("height",p.Height),new XElement("refresh_rate",p.Hz)));x.Save(path);
            }
        }
        static void Wait(Func<bool> ready,int seconds,string message) { var timer=Stopwatch.StartNew();while(timer.Elapsed.TotalSeconds<seconds){if(ready())return;Thread.Sleep(400);}throw new Exception(message); }
        static string SunshineDisplayId(string display) {
            string raw;
            using(var stream=new FileStream(Path.Combine(Sunshine,@"config\sunshine.log"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
            using(var reader=new StreamReader(stream))raw=reader.ReadToEnd();
            var matches=Regex.Matches(raw,@"(?s)Currently available display devices:\s*(\[.*?\])");
            if(matches.Count==0)return null;
            var serializer=new JavaScriptSerializer(); var devices=serializer.DeserializeObject(matches[matches.Count-1].Groups[1].Value) as object[];
            if(devices==null)return null;
            // Sunshine versions can log the display name with extra escaping. Bind by
            // the unique VDD identity rather than the volatile/escaped DISPLAY number.
            var matchesVdd=devices.OfType<Dictionary<string,object>>().Where(d=>Convert.ToString(d["friendly_name"]).IndexOf("VDD",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
            return matchesVdd.Length==1?Convert.ToString(matchesVdd[0]["device_id"]):null;
        }
        public static void Normal(Action<string> progress) {
            var primary=Primary();var before=DisplaySetup.GetMode(primary.name,-1);
            Report(progress,"正在收回虚拟屏窗口并恢复鼠标边界…");
            var v=Virtual();if(v.Length>0){Native.MoveWindows(v.Select(d=>DisplaySetup.GetMode(d.name,-1)).ToArray(),before);}
            DeviceCommand("disable");Wait(()=>Virtual().Length==0,12,"虚拟屏尚未从桌面断开。");CheckPrimary(primary.name,before);
            Report(progress,"正在停止 Sunshine 和 Deskflow…");
            StopService("SunshineService");StopService("Deskflow");
            StopApps(Sunshine,"sunshine","sunshinesvc");StopApps(Deskflow,"deskflow","deskflow-core","deskflow-daemon");
            Report(progress,"普通模式：虚拟屏已关闭，Sunshine 和 Deskflow 已停止。");
        }
        public static void Extend(Preset p,Action<string> progress) {
            p.Validate();var primary=Primary();var before=DisplaySetup.GetMode(primary.name,-1);
            try {
                Normal(progress);p.Save();EnsureMode(p);
                Report(progress,"正在启用虚拟屏…");DeviceCommand("enable");Wait(()=>Virtual().Length==1,20,"没有识别到唯一的 VDD 虚拟屏。");
                var v=Virtual().Single();if((v.flags&4)!=0)throw new Exception("VDD 被设置成主屏，请先修正。");
                var mode=DisplaySetup.GetMode(v.name,-1);mode.width=(uint)p.Width;mode.height=(uint)p.Height;mode.hz=(uint)p.Hz;mode.x=before.x-p.Width;mode.y=before.y;mode.fields=0x580020;
                int code=DisplaySetup.ChangeDisplaySettingsEx(v.name,ref mode,IntPtr.Zero,2,IntPtr.Zero);if(code!=0)throw new Exception("虚拟屏不支持此模式，代码 "+code);
                code=DisplaySetup.ChangeDisplaySettingsEx(v.name,ref mode,IntPtr.Zero,1,IntPtr.Zero);if(code!=0)throw new Exception("虚拟屏模式应用失败，代码 "+code);
                CheckPrimary(primary.name,before);
                Report(progress,"正在启动 Sunshine 并核对捕获目标…");
                string log=Path.Combine(Sunshine,@"config\sunshine.log");DateTime start=DateTime.Now;StartService("SunshineService");string id=null;
                string lastReadError=null;
                Wait(()=>{try {id=SunshineDisplayId(v.name);return !String.IsNullOrEmpty(id) && File.GetLastWriteTime(log)>=start.AddSeconds(-1);}catch(Exception readError){if(readError.Message!=lastReadError){lastReadError=readError.Message;Log("等待 Sunshine 日志："+readError.Message);}return false;}},55,"Sunshine 尚未报告当前虚拟屏。");
                string conf=Path.Combine(Sunshine,@"config\sunshine.conf");string content=File.ReadAllText(conf);var match=Regex.Match(content,@"(?m)^output_name\s*=\s*(.+?)\s*$");
                if(!match.Success || match.Groups[1].Value.Trim()!=id) {
                    Backup(conf);content=Regex.Replace(content,@"(?m)^output_name\s*=.*\r?\n?","").TrimEnd()+"\r\noutput_name = "+id+"\r\n";File.WriteAllText(conf,content,new UTF8Encoding(false));StopService("SunshineService");StartService("SunshineService");
                }
                Wait(()=>{try{using(var client=new System.Net.Sockets.TcpClient()){var t=client.ConnectAsync("127.0.0.1",47990);return t.Wait(700)&&client.Connected;}}catch{return false;}},55,"Sunshine 管理端口尚未就绪。");
                var final=DisplaySetup.GetMode(Virtual().Single().name,-1);if(final.width!=p.Width||final.height!=p.Height||final.hz!=p.Hz)throw new Exception("虚拟屏参数未保持，可能被其他程序更改。");
                CheckPrimary(primary.name,before);Report(progress,"扩展屏已就绪。请在 Mac 用匹配参数连接 Moonlight Desktop。");
            }catch(Exception error){Log(error.ToString());try{Normal(progress);}catch(Exception restore){Log("恢复失败："+restore);throw new Exception(error.Message+"\r\n恢复普通模式也失败，请查看 control.log。");}throw new Exception(error.Message+"\r\n已回退普通模式。");}
        }
        public static string DeskName { get {
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),@"Deskflow\Deskflow.conf");
            var match=Regex.Match(File.ReadAllText(path),@"(?m)^computerName\s*=\s*(.+?)\s*$");return match.Success?match.Groups[1].Value:Environment.MachineName;
        } }
        public static int DeskPort { get {
            string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),@"Deskflow\Deskflow.conf");
            var match=Regex.Match(File.ReadAllText(path),@"(?m)^port\s*=\s*(\d+)\s*$");int port;
            if(!match.Success)return 24800;
            if(!Int32.TryParse(match.Groups[1].Value,out port)||port<1||port>65535)throw new Exception("Deskflow 端口配置无效。");return port;
        } }
        public static bool DeskListening(){int port=DeskPort;return System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e=>e.Port==port&&!System.Net.IPAddress.IsLoopback(e.Address));}
        public static void Desk(Action<string> progress) {
            Normal(progress);Report(progress,"正在启动 Deskflow…");
            // No redirected pipes: Deskflow must survive closing this control panel.
            string config=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),@"Deskflow\Deskflow.conf");
            if(!File.Exists(config))throw new Exception("未找到现有 Deskflow 配置，请先在 Deskflow 设置 Windows 为服务端。");
            var info=new ProcessStartInfo(Path.Combine(Deskflow,"deskflow-core.exe"),"server --settings \""+config+"\"") { UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Deskflow };
            var process=new Process {StartInfo=info};
            try{process.Start();Wait(()=>{if(process.HasExited)throw new Exception("Deskflow 启动失败，代码 "+process.ExitCode+"。查看 control.log。");return DeskListening();},12,"Deskflow 未在局域网端口 "+DeskPort+" 开始监听，请检查原应用服务端配置。");Thread.Sleep(300);if(process.HasExited)throw new Exception("Deskflow 服务端已退出，请检查原应用配置。");}finally{process.Dispose();}
            // A successful listener is not proof that the Mac is connected.
            Report(progress,"Deskflow 已启动；Mac 是否连接请看状态/实际跨屏测试。若锁屏检查 Scroll Lock。");
        }
        public static string Status() {
            var v=Virtual();string screen=v.Length==0?"关闭":String.Join("，",v.Select(d=>{var m=DisplaySetup.GetMode(d.name,-1);return m.width+"×"+m.height+" / "+m.hz+"Hz";}));
            bool desk=Process.GetProcessesByName("deskflow-core").Any();
            return "虚拟屏："+screen+"\r\nSunshine："+(ServiceState("SunshineService")=="Running"?"运行":"停止")+"    Deskflow："+(desk?"运行":"停止");
        }
        public static void Setup() {
            string backup=Path.Combine(Home,"backups");Directory.CreateDirectory(backup);
            string record=Path.Combine(backup,"original-services.xml");if(!File.Exists(record)) {
                var root=new XElement("services");foreach(var name in new[]{"SunshineService","Deskflow"})using(var k=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\"+name))root.Add(new XElement("service",new XAttribute("name",name),new XAttribute("start",Convert.ToString(k.GetValue("Start")))));new XDocument(root).Save(record);
            }
            foreach(var name in new[]{"SunshineService","Deskflow"})if(Run(Path.Combine(Environment.SystemDirectory,"sc.exe"),"config "+name+" start= demand")!=0)throw new Exception("无法改为按需启动："+name);
            Backup(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),@"Deskflow\Deskflow.conf"));
            string cfg=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),@"Deskflow\Deskflow.conf");string body=File.ReadAllText(cfg);
            body=Regex.Replace(body,@"(?m)^startCoreWithGui=.*$","startCoreWithGui=false");File.WriteAllText(cfg,body,new UTF8Encoding(false));Normal(null);
        }
    }
    static class Native {
        [StructLayout(LayoutKind.Sequential)] struct Rect {public int left,top,right,bottom;}
        delegate bool EnumProc(IntPtr hwnd,IntPtr param);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr param);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int w,int h,uint flags);
        static Rectangle Bounds(DisplaySetup.Mode m){return new Rectangle(m.x,m.y,(int)m.width,(int)m.height);}
        public static void MoveWindows(DisplaySetup.Mode[] virtualModes,DisplaySetup.Mode primary) {
            var physical=DisplaySetup.Devices("").Where(d=>(d.flags&1)!=0&&d.description!="Virtual Display Driver").Select(d=>Bounds(DisplaySetup.GetMode(d.name,-1))).ToArray();int offset=40;
            EnumWindows((hwnd,param)=>{try {Rect r;if(!IsWindowVisible(hwnd)||IsIconic(hwnd)||!GetWindowRect(hwnd,out r))return true;var bounds=Rectangle.FromLTRB(r.left,r.top,r.right,r.bottom);
                if(bounds.Width<80||bounds.Height<40||!virtualModes.Any(m=>Bounds(m).IntersectsWith(bounds))||physical.Any(m=>m.IntersectsWith(bounds)))return true;
                SetWindowPos(hwnd,IntPtr.Zero,primary.x+offset,primary.y+offset,Math.Min(bounds.Width,(int)primary.width-100),Math.Min(bounds.Height,(int)primary.height-140),0x14);offset=offset>=160?40:offset+20;
            }catch{}return true;},IntPtr.Zero);
        }
    }
}

