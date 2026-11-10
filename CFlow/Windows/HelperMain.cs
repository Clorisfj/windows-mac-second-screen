using System;
using System.Diagnostics;
using System.IO;
namespace CFlow {
 static class HelperMain {
  [STAThread] static int Main(string[] args){try{
   if(args.Length!=2||args[0]!="--owner")return 1;int pid=Int32.Parse(args[1]);
   using(var owner=Process.GetProcessById(pid)){if(!String.Equals(owner.MainModule.FileName,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"CFlow.exe"),StringComparison.OrdinalIgnoreCase))return 1;}
   return Admin.Serve(pid);
  }catch(Exception e){ScreenDesk.Engine.Log("管理员助手异常："+e);return 1;}}
 }
}
