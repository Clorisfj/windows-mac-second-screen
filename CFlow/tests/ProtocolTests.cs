using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Net.NetworkInformation;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using CFlow;

class ProtocolTests {
 static int count;
 static void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static void Reject(Action action,string name){bool failed=false;try{action();}catch{failed=true;}Check(failed,name);}
 static string Token=Convert.ToBase64String(Enumerable.Repeat((byte)7,32).ToArray());
 static int Main(string[] args){try{
  RunDesktopConfigTests();
  RunDisplayPresetTests();
  Check(Peer.IsPrivate(IPAddress.Parse("10.2.3.4")),"10/8 accepted");
  Check(Peer.IsPrivate(IPAddress.Parse("172.31.2.1")),"172 private boundary accepted");
  Check(!Peer.IsPrivate(IPAddress.Parse("172.32.2.1")),"172 public boundary rejected");
  Check(!Peer.IsPrivate(IPAddress.Parse("8.8.8.8")),"public peer rejected");
  Check(!Peer.IsPrivate(IPAddress.IPv6Loopback),"unsupported IPv6 rejected");
  Reject(()=>Json.Read(new string('x',65537)),"oversized message rejected");
  Reject(()=>new Settings().Import("not-a-pair-code"),"malformed pair code rejected");
  Check(!Json.Read(Json.Write(new Settings())).ContainsKey("Token"),"unpaired settings serialize without reading a secret");
  var ip=NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up).SelectMany(n=>n.GetIPProperties().UnicastAddresses).Select(a=>a.Address).FirstOrDefault(Peer.IsPrivate);
  if(ip==null)throw new Exception("A private IPv4 interface is needed for local TLS tests.");
  var certificate=new X509Certificate2(args[0],"cflow-tests-only");string pin;
  using(var sha=SHA256.Create())pin=BitConverter.ToString(sha.ComputeHash(certificate.RawData)).Replace("-","");
  Check(Peer.PinMatches(certificate,pin),"certificate fingerprint accepted");
  Check(!Peer.PinMatches(certificate,new string('0',64)),"wrong certificate fingerprint rejected");
  var listener=new TcpListener(ip,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;
  string pairing="cflow1:"+Convert.ToBase64String(Encoding.UTF8.GetBytes(Json.Write(new{host=ip.ToString(),port=port,name="Local test fixture",fingerprint=pin,token=Token})));
  var settings=new Settings().Import(pairing);Check(settings.Token==Token&&settings.Paired,"pair code imported with user-bound encryption");
  Check(!Json.Read(Json.Write(settings)).ContainsKey("Token")&&!Json.Write(settings).Contains(Token),"settings never serialize the plaintext control secret");
  var server=Task.Run(()=>{
   for(int i=0;i<4;i++)using(var tcp=listener.AcceptTcpClient())try{
    using(var tls=new SslStream(tcp.GetStream(),false)){
     tls.ReadTimeout=5000;tls.WriteTimeout=5000;tls.AuthenticateAsServer(certificate,false,SslProtocols.Tls12,false);
     var reader=new StreamReader(tls,Encoding.UTF8,false,4096,true);var writer=new StreamWriter(tls,new UTF8Encoding(false),4096,true){AutoFlush=true};var request=Json.Read(Json.Line(reader));
     if(Json.Text(request,"token")!=Token){writer.WriteLine(Json.Write(new{ok=false,id=Json.Text(request,"id"),error="fixture authentication rejected"}));continue;}
     if(Convert.ToInt32(request["width"])!=2880||Json.Text(request,"windowsHost")!=ip.ToString())throw new Exception("Bad request routing or preset");
     writer.WriteLine(Json.Write(new{ok=true,id=i==3?"wrong-id":Json.Text(request,"id"),mode="normal"}));
    }
   }catch(IOException){}catch(AuthenticationException){}
  });
  Check(Json.Text(Peer.Call(settings,"status"),"mode")=="normal","TLS request/response and routing round trip");
  string original=settings.PeerFingerprint;settings.PeerFingerprint=new string('0',64);Reject(()=>Peer.Call(settings,"status"),"live TLS pin mismatch fails closed");settings.PeerFingerprint=original;
  string saved=settings.ProtectedToken;settings.ProtectedToken=Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(Convert.ToBase64String(new byte[32])),null,DataProtectionScope.CurrentUser));Reject(()=>Peer.Call(settings,"status"),"server authentication rejection surfaced");settings.ProtectedToken=saved;
  Reject(()=>Peer.Call(settings,"status"),"response request ID mismatch rejected");
  if(!server.Wait(10000))throw new Exception("TLS fixture did not finish");listener.Stop();certificate.Dispose();
  settings.PeerHost="8.8.8.8";Reject(()=>Peer.Call(settings,"status"),"public host blocked before connection");
  RunPairingTests(certificate: new X509Certificate2(args[0],"cflow-tests-only"),ip:ip);
  RunRequestFailureTest(new X509Certificate2(args[0],"cflow-tests-only"),ip);
  Console.WriteLine("TOTAL "+count+" passed");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
 static void RunDisplayPresetTests(){
  var flags=System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic;
  var addModes=typeof(ScreenDesk.Engine).GetMethod("AddPresetModes",flags);
  var settings=System.Xml.Linq.XDocument.Parse("<settings><name>keep</name><resolutions><resolution><width>1920</width><height>1200</height><refresh_rate>60</refresh_rate></resolution></resolutions></settings>");
  Check((bool)addModes.Invoke(null,new object[]{settings}),"missing UI presets are registered before enabling VDD");
  var resolutions=settings.Root.Element("resolutions").Elements("resolution").ToArray();
  Check(resolutions.Length==3&&resolutions.All(r=>r.Elements("refresh_rate").Select(h=>(int)h).OrderBy(h=>h).SequenceEqual(new[]{30,60})),"all three resolutions expose 30 and 60 Hz without duplicate resolutions");
  Check((string)settings.Root.Element("name")=="keep","preset registration preserves unrelated VDD settings");
  Check(!(bool)addModes.Invoke(null,new object[]{settings}),"complete VDD presets are not rewritten");
  Reject(()=>addModes.Invoke(null,new object[]{System.Xml.Linq.XDocument.Parse("<settings/>")}),"invalid VDD configuration fails closed");
  var matches=typeof(ScreenDesk.Engine).GetMethod("MatchesVirtualMode",flags);
  var preset=new ScreenDesk.Preset{Width=2560,Height=1600,Hz=30};
  var primary=new DisplaySetup.Mode{x=100,y=20,width=2560,height=1440,hz=180};
  var mode=new DisplaySetup.Mode{x=-2460,y=20,width=2560,height=1600,hz=30};
  Func<bool> match=()=>(bool)matches.Invoke(null,new object[]{mode,preset,primary});
  Check(match(),"hot reconfiguration matches target resolution refresh and relative position");
  mode.hz=60;Check(!match(),"refresh changes cannot reuse an unmodified mode");mode.hz=30;
  mode.width=1920;Check(!match(),"resolution changes cannot reuse an unmodified mode");mode.width=2560;
  mode.x=0;Check(!match(),"unexpected horizontal layout cannot pass readiness");mode.x=-2460;
  mode.y=0;Check(!match(),"unexpected vertical layout cannot pass readiness");
 }
 static void RunDesktopConfigTests(){
  var method=typeof(ScreenDesk.Engine).GetMethod("DesktopSunshineConfig",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
  Func<string,string> prepare=content=>(string)method.Invoke(null,new object[]{content});
  string original="output_name = {existing-display}\r\nlocale = zh\r\n";
  string result=prepare(original);
  Check(result.StartsWith(original)&&result.Contains("controller = disabled"),"desktop startup skips default gamepad probes and preserves existing settings");
  Check(prepare(result)==result,"desktop startup configuration is idempotent");
  Check(prepare("controller = enabled\r\n").StartsWith("controller = enabled\r\n")&&!prepare("controller = enabled\r\n").Contains("controller = disabled"),"explicit enabled controller preference is preserved");
  Check(prepare("\tcontroller = disabled\n").StartsWith("\tcontroller = disabled\n"),"explicit indented controller preference is preserved");
  Check(result.Contains("system_tray = disabled"),"CFlow avoids Sunshine tray initialization and shutdown waits");
  Check(prepare("system_tray = enabled\n").StartsWith("system_tray = enabled\n")&&!prepare("system_tray = enabled\n").Contains("system_tray = disabled"),"explicit Sunshine tray preference is preserved");
  string explicitOptions="controller = enabled\nsystem_tray = enabled\n";
  Check(prepare(explicitOptions)==explicitOptions,"all explicit Sunshine preferences remain unchanged");
  Check(prepare("# controller = enabled").Contains("\r\ncontroller = disabled"),"commented preference does not enable gamepad probes");
  Check(prepare("locale = zh").StartsWith("locale = zh\r\n# CFlow"),"configuration without trailing newline remains valid");
 }
 static void RunPairingTests(X509Certificate2 certificate,IPAddress ip){
  string nonce=Convert.ToBase64String(Enumerable.Repeat((byte)7,32).ToArray());
  string serverNonce=Convert.ToBase64String(Enumerable.Repeat((byte)9,32).ToArray());
  Check(Pairing.Commitment(nonce)=="EBBAD97F478755D90C8F34BEC04444DC8B0025D5FC09ED3523AA5D17E6059D86","pairing commitment matches shared Swift vector");
  Check(Pairing.VerificationCode(new string('A',64),nonce,serverNonce)=="398057","pairing verification code matches shared Swift vector");
  Reject(()=>Pairing.Begin("8.8.8.8"),"IP pairing rejects public address");
  var listener=new TcpListener(ip,47999);listener.Start();string commitment="",clientNonce="";
  var server=Task.Run(()=>{
   for(int i=0;i<7;i++)using(var tcp=listener.AcceptTcpClient())try{
    using(var tls=new SslStream(tcp.GetStream(),false)){
     tls.ReadTimeout=5000;tls.WriteTimeout=5000;tls.AuthenticateAsServer(certificate,false,SslProtocols.Tls12,false);
     var reader=new StreamReader(tls);var writer=new StreamWriter(tls,new UTF8Encoding(false)){AutoFlush=true};var request=Json.Read(Json.Line(reader));
     string action=Json.Text(request,"action");var response=new Dictionary<string,object>{{"id",Json.Text(request,"id")},{"ok",true}};
     if(i==0){if(action!="pair.begin"||request.ContainsKey("clientNonce"))throw new Exception("Nonce must remain committed until server challenge");commitment=Json.Text(request,"clientCommit");response["session"]=new string('a',32);response["serverNonce"]=serverNonce;response["name"]="Test Mac";}
     else if(i==1){clientNonce=Json.Text(request,"clientNonce");if(action!="pair.verify"||Pairing.Commitment(clientNonce)!=commitment)throw new Exception("Commitment mismatch");}
     else if(i==2){response["state"]="pending";}
     else if(i==4){response["state"]="approved";response["token"]=Token;}
     else if(i==5){response["ok"]=false;response["error"]="Pairing denied";}
     else if(i==6){response["id"]="wrong-id";response["state"]="approved";response["token"]=Token;}
     writer.WriteLine(Json.Write(response));
    }
   }catch(IOException){}catch(AuthenticationException){}
  });
  try{
   var session=Pairing.Begin(ip.ToString());
   Check(session.Code==Pairing.VerificationCode(session.Fingerprint,session.ClientNonce,serverNonce),"IP pairing derives code from observed TLS certificate");
   Check(Pairing.Complete(new Settings(),session)==null,"unapproved pairing never supplies a saved configuration");
   string original=session.Fingerprint;session.Fingerprint=new string('0',64);Reject(()=>Pairing.Complete(new Settings(),session),"pairing refuses certificate change after discovery");session.Fingerprint=original;
   var candidate=Pairing.Complete(new Settings(),session);
   Check(candidate.Paired&&candidate.Token==Token,"approved IP pairing creates encrypted configuration");
   Check(!Json.Write(candidate).Contains(Token),"IP pairing never serializes plaintext control secret");
   Reject(()=>Pairing.Complete(new Settings(),session),"denied pairing surfaced to caller");
   Reject(()=>Pairing.Complete(new Settings(),session),"pairing rejects response ID mismatch");
   Check(server.Wait(10000),"pairing fixture completes");
  }finally{listener.Stop();certificate.Dispose();}
 }
 static void RunRequestFailureTest(X509Certificate2 certificate,IPAddress ip){
  string pin;using(var hash=SHA256.Create())pin=BitConverter.ToString(hash.ComputeHash(certificate.RawData)).Replace("-","");
  var listener=new TcpListener(ip,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;
  var settings=new Settings().Import("cflow1:"+Convert.ToBase64String(Encoding.UTF8.GetBytes(Json.Write(new{host=ip.ToString(),port=port,fingerprint=pin,token=Token,name="Request fixture"}))));
  var server=Task.Run(()=>{
   for(int i=0;i<3;i++)using(var tcp=listener.AcceptTcpClient())using(var tls=new SslStream(tcp.GetStream(),false)){
    tls.AuthenticateAsServer(certificate,false,SslProtocols.Tls12,false);var reader=new StreamReader(tls);var writer=new StreamWriter(tls,new UTF8Encoding(false)){AutoFlush=true};var request=Json.Read(Json.Line(reader));
    if(i==1&&Json.Text(request,"requestState")!="running")throw new Exception("Missing running acknowledgement");
    if(i==2&&(Json.Text(request,"requestState")!="failed"||Json.Text(request,"requestError").Length==0))throw new Exception("Missing failure result");
    writer.WriteLine(Json.Write(new{ok=true,id=Json.Text(request,"id"),requestedMode=i==0?"extend":"",requestID="fixture-request",mode="normal",width=1,height=1,fps=60,bitrate=40,audioOnHost=true}));
   }
  });
  try{var flow=new Coordinator(settings);flow.Poll();Check(flow.Online&&flow.Mode=="normal"&&flow.PeerState.StartsWith("切换失败"),"local request failure does not label reachable Mac offline");Check(server.Wait(10000),"request result fixture completes");}
  finally{listener.Stop();certificate.Dispose();}
 }
}
