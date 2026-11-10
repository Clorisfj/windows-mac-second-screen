using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using CFlow;

class AdminPipeTests {
 static int count;
 static string Pipe {get{return "CFlow-"+WindowsIdentity.GetCurrent().User.Value.Replace('-','_');}}
 static void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static string Call(string action){using(var pipe=new NamedPipeClientStream(".",Pipe,PipeDirection.InOut)){
  pipe.Connect(2000);var writer=new StreamWriter(pipe){AutoFlush=true};writer.WriteLine(Json.Write(new{command=action}));
  var reader=new StreamReader(pipe);var read=Task.Run(()=>Json.Line(reader));if(!read.Wait(3000))throw new Exception("Pipe response timed out");return read.Result;
 }}
 static bool Ok(string action){return Convert.ToBoolean(Json.Read(Call(action))["ok"]);}
 static int Main(string[] args){try{
  if(args.Length>0&&args[0]=="--foreign"){try{Call("ping");return 1;}catch{return 0;}}
  string log=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"control.log");DateTime before=File.Exists(log)?File.GetLastWriteTimeUtc(log):DateTime.MinValue;
  var server=Task.Run(()=>Admin.Serve(Process.GetCurrentProcess().Id));
  Check(Ok("ping"),"owner process receives helper readiness response");
  Thread.Sleep(1500);Check(Ok("ping"),"helper survives idle connection wait");
  Check(Ok("ping")&&Ok("ping"),"repeated connections remain usable");
  Check(!Ok("unsupported"),"unknown actions rejected");
  Check(Ok("ping"),"helper remains usable after rejected action");
  using(var foreign=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--foreign"){UseShellExecute=false,CreateNoWindow=true})){
   Check(foreign.WaitForExit(5000)&&foreign.ExitCode==0,"another process cannot issue commands");
  }
  Check(Ok("ping"),"owner reconnects after foreign client rejection");
  Check(Ok("shutdown"),"shutdown acknowledged");Check(server.Wait(3000),"helper server exits cleanly");
  Check((File.Exists(log)?File.GetLastWriteTimeUtc(log):DateTime.MinValue)==before,"readiness-only helper never runs display or service cleanup");
  Console.WriteLine("TOTAL "+count+" passed");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
