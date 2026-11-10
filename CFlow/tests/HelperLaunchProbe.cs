using System;
using System.Threading;
using CFlow;

class HelperLaunchProbe {
 static int Main(){try{
  Admin.Call("ping",new Settings());Console.WriteLine("PASS shipped helper launches and authenticates its owner");
  Thread.Sleep(1500);Admin.Call("ping",new Settings());Console.WriteLine("PASS shipped helper remains ready after idle wait");
  Admin.Shutdown();Console.WriteLine("PASS shipped helper accepts owner shutdown");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{Admin.Shutdown();}}
}
