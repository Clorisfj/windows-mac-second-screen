using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class DisplaySetup {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct Device { public int cb; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string name; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string description; public uint flags; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string id; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string key; }
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct Mode { [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string device; public ushort spec,driver,size,extra; public uint fields; public int x,y; public uint orientation,fixedOutput; public short color,duplex,yres,tt,collate; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string form; public ushort logPixels; public uint bits,width,height,flags,hz,icmMethod,icmIntent,media,dither,reserved1,reserved2,panningWidth,panningHeight; }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern bool EnumDisplayDevices(string parent,uint index,ref Device device,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern bool EnumDisplaySettings(string device,int index,ref Mode mode);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int ChangeDisplaySettingsEx(string device,ref Mode mode,IntPtr hwnd,uint flags,IntPtr param);
 public static Mode GetMode(string device,int index) { var m=new Mode(); m.size=(ushort)Marshal.SizeOf(typeof(Mode)); if(!EnumDisplaySettings(device,index,ref m)) throw new Exception("Cannot read mode"); return m; }
 public static Device[] Devices(string parent) { var result=new List<Device>(); for(uint i=0;;i++){var d=new Device();d.cb=Marshal.SizeOf(typeof(Device));if(!EnumDisplayDevices(String.IsNullOrEmpty(parent)?null:parent,i,ref d,0))break;result.Add(d);}return result.ToArray(); }
 public static Mode[] Modes(string device) {var list=new List<Mode>();for(int i=0;;i++){var m=new Mode();m.size=(ushort)Marshal.SizeOf(typeof(Mode));if(!EnumDisplaySettings(device,i,ref m))break;list.Add(m);}return list.ToArray();}
}

