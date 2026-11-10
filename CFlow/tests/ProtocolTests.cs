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
  Console.WriteLine("TOTAL "+count+" passed");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
