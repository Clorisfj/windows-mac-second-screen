using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Security.AccessControl;
using System.IO.Pipes;
using System.Threading;
using System.Web.Script.Serialization;
using ScreenDesk;

namespace CFlow {
 public static class Json {
  public static string Write(object value){return new JavaScriptSerializer().Serialize(value);}
  public static Dictionary<string,object> Read(string value){if(value.Length>65536)throw new Exception("消息过长。");return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(value);}
  public static string Text(Dictionary<string,object> value,string key){return value.ContainsKey(key)?Convert.ToString(value[key]):"";}
  public static string Line(TextReader reader){var b=new StringBuilder();for(int i=0;i<65536;i++){int c=reader.Read();if(c<0)throw new EndOfStreamException();if(c==10)return b.ToString();if(c!=13)b.Append((char)c);}throw new Exception("消息超过限制。");}
 }
 public sealed class Settings {
  public int Width=2880,Height=1800,Fps=60,Bitrate=40,PeerPort=47999;
  public string PeerHost="",PeerName="Mac",PeerFingerprint="",ProtectedToken="";
  public bool AudioOnHost=true,CloseToTray=true;
  public static string Folder {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CFlow");}}
  public static string FileName {get{return Path.Combine(Folder,"settings.json");}}
  public static Settings Load(){try{return new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(FileName))??new Settings();}catch{return new Settings();}}
  public void Save(){
   lock(typeof(Settings)){
    Directory.CreateDirectory(Folder);
    string temp=FileName+".tmp";File.WriteAllText(temp,Json.Write(this),new UTF8Encoding(false));
    if(File.Exists(FileName))File.Replace(temp,FileName,FileName+".bak");else File.Move(temp,FileName);
   }
  }

  [ScriptIgnore] public string Token {get {return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ProtectedToken),null,DataProtectionScope.CurrentUser));}}
  [ScriptIgnore] public bool Paired {get{return PeerHost.Length>0&&PeerFingerprint.Length==64&&ProtectedToken.Length>0;}}
  public Preset Preset(){return new Preset{Width=Width,Height=Height,Hz=Fps,Bitrate=Bitrate};}
  public Settings Import(string code){
   if(!code.Trim().StartsWith("cflow1:"))throw new Exception("请粘贴 Mac CFlow 生成的完整配对码。");
   var d=Json.Read(Encoding.UTF8.GetString(Convert.FromBase64String(code.Trim().Substring(7))));
   string host=Json.Text(d,"host"),pin=Json.Text(d,"fingerprint").ToUpperInvariant(),token=Json.Text(d,"token");
   int port=Convert.ToInt32(d["port"]);IPAddress address;
   if(!IPAddress.TryParse(host,out address)||!Peer.IsPrivate(address)||port<1024||port>65535||!System.Text.RegularExpressions.Regex.IsMatch(pin,"^[0-9A-F]{64}$")||Convert.FromBase64String(token).Length!=32)throw new Exception("配对码无效，或地址不在局域网。");
   return new Settings{Width=Width,Height=Height,Fps=Fps,Bitrate=Bitrate,AudioOnHost=AudioOnHost,CloseToTray=CloseToTray,PeerHost=host,PeerPort=port,PeerName=Json.Text(d,"name"),PeerFingerprint=pin,ProtectedToken=Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(token),null,DataProtectionScope.CurrentUser))};
  }
  public void ResetPreferences(){Width=2880;Height=1800;Fps=60;Bitrate=40;AudioOnHost=true;CloseToTray=true;}
 }
 public static class Peer {
  public static bool IsPrivate(IPAddress address){var b=address.GetAddressBytes();return b.Length==4&&(b[0]==10||(b[0]==172&&b[1]>=16&&b[1]<=31)||(b[0]==192&&b[1]==168));}
  public static bool PinMatches(X509Certificate certificate,string pin){if(certificate==null)return false;using(var hash=SHA256.Create()){return BitConverter.ToString(hash.ComputeHash(certificate.GetRawCertData())).Replace("-","").Equals(pin,StringComparison.OrdinalIgnoreCase);}}
  internal static Dictionary<string,object> Exchange(string host,int port,string pin,Dictionary<string,object> request,Action<string> discovered){
   IPAddress ip;
   if(!IPAddress.TryParse(host,out ip)||!IsPrivate(ip))throw new Exception("请输入 Mac 的局域网 IPv4 地址。");
   using(var tcp=new TcpClient()){
    var connecting=tcp.ConnectAsync(ip,port);
    if(!connecting.Wait(3000))throw new Exception("无法连接 Mac，请检查 IP 并打开 Mac CFlow。");
    connecting.GetAwaiter().GetResult();
    using(var tls=new SslStream(tcp.GetStream(),false,(sender,cert,chain,errors)=>{
     if(discovered==null)return PinMatches(cert,pin);
     if(cert==null)return false;
     using(var hash=SHA256.Create())discovered(BitConverter.ToString(hash.ComputeHash(cert.GetRawCertData())).Replace("-",""));
     return true;
    })){
     tls.ReadTimeout=15000;tls.WriteTimeout=5000;
     var handshake=tls.AuthenticateAsClientAsync("CFlow",null,SslProtocols.Tls12,false);
     if(!handshake.Wait(5000))throw new Exception("Mac 的安全连接握手超时。");
     handshake.GetAwaiter().GetResult();
     string id=Guid.NewGuid().ToString("N");request["id"]=id;
     request["windowsHost"]=((IPEndPoint)tcp.Client.LocalEndPoint).Address.ToString();
     var writer=new StreamWriter(tls,new UTF8Encoding(false),4096,true){AutoFlush=true};
     var reader=new StreamReader(tls,Encoding.UTF8,false,4096,true);
     writer.WriteLine(Json.Write(request));var response=Json.Read(Json.Line(reader));
     if(Json.Text(response,"id")!=id)throw new Exception("Mac 回复与当前请求不匹配。");
     if(!response.ContainsKey("ok")||!Convert.ToBoolean(response["ok"]))throw new Exception(Json.Text(response,"error"));
     return response;
    }
   }
  }
  public static Dictionary<string,object> Call(Settings s,string action,string requestId="",string requestState="",string requestError=""){
   if(!s.Paired)throw new Exception("请先在设置中完成 CFlow 双端控制配对。");
   IPAddress ip;if(!IPAddress.TryParse(s.PeerHost,out ip)||!IsPrivate(ip))throw new Exception("只允许连接已配对的局域网地址。");
   return Exchange(s.PeerHost,s.PeerPort,s.PeerFingerprint,new Dictionary<string,object>{{"token",s.Token},{"action",action},{"width",s.Width},{"height",s.Height},{"fps",s.Fps},{"bitrate",s.Bitrate},{"audioOnHost",s.AudioOnHost},{"requestID",requestId},{"requestState",requestState},{"requestError",requestError}},null);
  }
 }
 public sealed class PairingSession {
  public string Host,Fingerprint,ClientNonce,SessionId,ServerNonce,Name,Code;
 }
 public static class Pairing {
  public static string Commitment(string nonce){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(nonce))).Replace("-","");}
  public static string VerificationCode(string fingerprint,string clientNonce,string serverNonce){
   using(var hash=SHA256.Create()){
    var bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(fingerprint.ToUpperInvariant()+"|"+clientNonce+"|"+serverNonce));
    uint value=((uint)bytes[0]<<24)|((uint)bytes[1]<<16)|((uint)bytes[2]<<8)|bytes[3];
    return (value%1000000).ToString("D6");
   }
  }
  public static PairingSession Begin(string host){
   var session=new PairingSession{Host=host.Trim()};
   byte[] nonce=new byte[32];using(var random=RandomNumberGenerator.Create())random.GetBytes(nonce);
   session.ClientNonce=Convert.ToBase64String(nonce);
   var response=Peer.Exchange(session.Host,47999,null,new Dictionary<string,object>{{"action","pair.begin"},{"clientCommit",Commitment(session.ClientNonce)}},pin=>session.Fingerprint=pin);
   session.SessionId=Json.Text(response,"session");session.ServerNonce=Json.Text(response,"serverNonce");session.Name=Json.Text(response,"name");
   if(session.SessionId.Length!=32||Convert.FromBase64String(session.ServerNonce).Length!=32)throw new Exception("Mac 配对响应无效，请更新两端 CFlow。");
   session.Code=VerificationCode(session.Fingerprint,session.ClientNonce,session.ServerNonce);
   Peer.Exchange(session.Host,47999,session.Fingerprint,new Dictionary<string,object>{{"action","pair.verify"},{"session",session.SessionId},{"clientNonce",session.ClientNonce}},null);
   return session;
  }
  public static Settings Complete(Settings current,PairingSession session){
   var response=Peer.Exchange(session.Host,47999,session.Fingerprint,new Dictionary<string,object>{{"action","pair.finish"},{"session",session.SessionId},{"clientNonce",session.ClientNonce}},null);
   if(Json.Text(response,"state")=="pending")return null;
   if(Json.Text(response,"state")!="approved")throw new Exception("Mac 没有允许此次配对。");
   string code="cflow1:"+Convert.ToBase64String(Encoding.UTF8.GetBytes(Json.Write(new{host=session.Host,port=47999,fingerprint=session.Fingerprint,token=Json.Text(response,"token"),name=session.Name})));
   return current.Import(code);
  }
 }
 public static class LocalNetwork {
  public static string Addresses(string peer){
   IPAddress address;if(IPAddress.TryParse(peer,out address)&&Peer.IsPrivate(address))try{using(var socket=new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp)){socket.Connect(new IPEndPoint(address,47999));return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();}}catch{}
   var addresses=System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==System.Net.NetworkInformation.OperationalStatus.Up).OrderByDescending(n=>n.GetIPProperties().GatewayAddresses.Count>0).SelectMany(n=>n.GetIPProperties().UnicastAddresses).Select(a=>a.Address).Where(Peer.IsPrivate).Select(a=>a.ToString()).Distinct().ToArray();
   return addresses.Length==0?"未找到局域网 IPv4 地址":String.Join(Environment.NewLine,addresses);
  }
 }
 public static class Startup {
  const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
  public static bool Enabled{get{using(var k=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Key))return k!=null&&k.GetValue("CFlow")!=null;}}
  public static void Set(bool enabled){using(var k=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(Key)){if(enabled)k.SetValue("CFlow","\""+System.Windows.Forms.Application.ExecutablePath+"\" --tray");else k.DeleteValue("CFlow",false);}}
 }
 public static class Admin {
  static string Pipe {get{return "CFlow-"+WindowsIdentity.GetCurrent().User.Value.Replace('-','_');}}
  static Process helper;
  static string Exchange(string data,int timeout){using(var pipe=new NamedPipeClientStream(".",Pipe,PipeDirection.InOut)){pipe.Connect(timeout);var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true};var reader=new StreamReader(pipe,Encoding.UTF8,false,4096,true);writer.WriteLine(data);return Json.Line(reader);}}
  public static void Call(string command,Settings settings){
   if(command=="normal") {
    bool display=DisplaySetup.Devices("").Any(d=>(d.flags&1)!=0&&d.description=="Virtual Display Driver");
    bool apps=new[]{"sunshine","sunshinesvc","deskflow","deskflow-core","deskflow-daemon"}.Any(n=>Process.GetProcessesByName(n).Length>0);
    if(!display&&!apps)return;
   }
   string data=Json.Write(new{command=command,width=settings.Width,height=settings.Height,fps=settings.Fps,bitrate=settings.Bitrate});
   try{Exchange(Json.Write(new{command="ping"}),300);}catch{
    string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CFlow.Helper.exe");helper=Process.Start(new ProcessStartInfo(path,"--owner "+Process.GetCurrentProcess().Id){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});
    bool ready=false;for(int i=0;i<30;i++){Thread.Sleep(200);try{Exchange(Json.Write(new{command="ping"}),200);ready=true;break;}catch{if(helper.HasExited)break;}}
    if(!ready)throw new Exception("管理员助手没有就绪，操作未执行。"+(helper.HasExited?"助手退出代码："+helper.ExitCode+"，请查看本地 control.log。":"请检查授权窗口是否已确认。"));
   }
   var result=Json.Read(Exchange(data,2000));if(!Convert.ToBoolean(result["ok"]))throw new Exception(Json.Text(result,"error"));
  }
  public static void Shutdown(){try{Exchange(Json.Write(new{command="shutdown"}),400);}catch{}}
  public static int Serve(int owner){
   var sid=WindowsIdentity.GetCurrent().User;var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);security.AddAccessRule(new PipeAccessRule(sid,PipeAccessRights.FullControl,AccessControlType.Allow));security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),PipeAccessRights.FullControl,AccessControlType.Allow));
   bool quit=false,managed=false;try{while(!quit){
    try{if(Process.GetProcessById(owner).HasExited)break;}catch{break;}
    using(var pipe=new NamedPipeServerStream(Pipe,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,security)){
     // The async wait handle belongs to the pipe's pending operation. Disposing
     // it before EndWaitForConnection closes a handle still used by .NET.
     var waiting=pipe.BeginWaitForConnection(null,null);
     while(!waiting.IsCompleted){try{if(Process.GetProcessById(owner).HasExited){quit=true;break;}}catch{quit=true;break;}Thread.Sleep(100);}
     if(quit)break;pipe.EndWaitForConnection(waiting);
     uint client; if(!GetNamedPipeClientProcessId(pipe.SafePipeHandle,out client)||client!=(uint)owner)continue;
     var reader=new StreamReader(pipe,Encoding.UTF8,false,4096,true);var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true};
     try{var reading=System.Threading.Tasks.Task.Run(()=>Json.Line(reader));if(!reading.Wait(5000))throw new Exception("本机请求读取超时。");var d=Json.Read(reading.Result);string command=Json.Text(d,"command");
      if(command=="shutdown")quit=true;
      else if(command=="normal"){managed=true;CloseLegacy();Engine.Normal(null);}
      else if(command=="extend"||command=="deskflow"){
       var p=new Preset{Width=Convert.ToInt32(d["width"]),Height=Convert.ToInt32(d["height"]),Hz=Convert.ToInt32(d["fps"]),Bitrate=Convert.ToInt32(d["bitrate"])};p.Validate();
       managed=true;CloseLegacy();if(command=="extend")Engine.Extend(p,null);else Engine.Desk(null);
      }else if(command!="ping")throw new Exception("不支持的控制命令。");
      writer.WriteLine(Json.Write(new{ok=true}));
     }catch(Exception e){try{writer.WriteLine(Json.Write(new{ok=false,error=e.Message}));}catch{}}
    }
   }}finally{if(managed)try{Engine.Normal(null);}catch(Exception e){Engine.Log("管理员助手退出收尾："+e.Message);}}return 0;
  }
  [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe,out uint processId);
  static void CloseLegacy(){foreach(var p in Process.GetProcessesByName("ScreenDesk"))using(p){string expected=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,@"..\ScreenDesk\ScreenDesk.exe"));if(!String.Equals(p.MainModule.FileName,expected,StringComparison.OrdinalIgnoreCase))continue;p.CloseMainWindow();if(!p.WaitForExit(40000))throw new Exception("旧版屏幕联动面板未退出，请关闭旧版后重试。");}}
 }
 public sealed class Coordinator {
  public Settings Settings;public string Mode="normal";public string PeerState="未连接";public bool Online;public bool Busy;DateTime lastOnline=DateTime.UtcNow;string lastMacRequest="";
  public Coordinator(Settings settings){Settings=settings;}
  public void Poll(){
   if(Busy||!Settings.Paired)return;
   Dictionary<string,object> response;
   try{response=Peer.Call(Settings,"status");Online=true;lastOnline=DateTime.UtcNow;PeerState="已连接";}
   catch(Exception error){
    Online=false;PeerState="Mac 未连接 · "+error.GetBaseException().Message;
    if(Mode!="normal"&&(DateTime.UtcNow-lastOnline).TotalSeconds>20){Admin.Call("normal",Settings);Mode="normal";}
    return;
   }
   string wanted=Json.Text(response,"requestedMode"),requestId=Json.Text(response,"requestID");
   if(requestId.Length>0&&requestId!=lastMacRequest&&(wanted=="normal"||wanted=="extend"||wanted=="deskflow")){
    lastMacRequest=requestId;
    try{
     Peer.Call(Settings,"status",requestId,"running");
     var preset=new Preset{Width=Convert.ToInt32(response["width"]),Height=Convert.ToInt32(response["height"]),Hz=Convert.ToInt32(response["fps"]),Bitrate=Convert.ToInt32(response["bitrate"])};
     preset.Validate();Settings.Width=preset.Width;Settings.Height=preset.Height;Settings.Fps=preset.Hz;Settings.Bitrate=preset.Bitrate;Settings.AudioOnHost=Convert.ToBoolean(response["audioOnHost"]);Settings.Save();
     Change(wanted,requestId);Peer.Call(Settings,"status",requestId,"done");
    }catch(Exception error){
     PeerState="切换失败 · "+error.GetBaseException().Message;
     try{Peer.Call(Settings,"status",requestId,"failed",error.GetBaseException().Message);}catch{}
    }
    return;
   }
   if(Mode!="normal"&&Json.Text(response,"mode")=="normal"){Admin.Call("normal",Settings);Mode="normal";}
   if(Mode=="deskflow"&&!Engine.DeskListening()){
    try{Peer.Call(Settings,"stop");}finally{Admin.Call("normal",Settings);Mode="normal";PeerState="Deskflow 服务端已停止";}
   }
  }
  void RunHost(string mode,string requestId){
   var operation=System.Threading.Tasks.Task.Run(()=>Admin.Call(mode,Settings));
   while(!operation.Wait(4000)){
    try{Peer.Call(Settings,"status",requestId,requestId.Length>0?"running":"");lastOnline=DateTime.UtcNow;Online=true;}
    catch{if(mode=="normal")continue;try{operation.GetAwaiter().GetResult();}catch{}throw;}
   }
   operation.GetAwaiter().GetResult();
  }
  public void Change(string mode,string requestId=""){if(mode!="normal"&&mode!="extend"&&mode!="deskflow")throw new Exception("无效模式。");Busy=true;var elapsed=Stopwatch.StartNew();
   Engine.Log("模式切换开始："+mode);
   try{
    if(mode=="normal"){Exception remote=null;if(Settings.Paired)try{Peer.Call(Settings,"stop");}catch(Exception e){remote=e;}RunHost("normal",requestId);Mode="normal";if(remote!=null)PeerState="本机已关闭，Mac 待确认";return;}
    Peer.Call(Settings,"status");Online=true;lastOnline=DateTime.UtcNow;Peer.Call(Settings,"stop");
    Engine.Log("模式切换耗时：Mac 停止确认 "+elapsed.ElapsedMilliseconds+"ms");
    try{RunHost(mode,requestId);Engine.Log("模式切换耗时：Windows 就绪 "+elapsed.ElapsedMilliseconds+"ms");Peer.Call(Settings,mode);Engine.Log("模式切换耗时：Mac 启动确认 "+elapsed.ElapsedMilliseconds+"ms");Mode=mode;PeerState=mode=="extend"?"Moonlight 已启动":"Deskflow 已启动";}
    catch{try{Peer.Call(Settings,"stop");}catch{}try{Admin.Call("normal",Settings);}catch{}Mode="normal";throw;}
   }finally{Busy=false;Engine.Log("模式切换结束："+mode+"，总计 "+elapsed.ElapsedMilliseconds+"ms");}
  }
 }
}
