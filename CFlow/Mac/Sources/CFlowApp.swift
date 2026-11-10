import SwiftUI
import AppKit
import ServiceManagement

@MainActor final class AppDelegate:NSObject,NSApplicationDelegate {
    func applicationWillTerminate(_ notification:Notification) {FlowModel.instance?.emergencyStop()}
}
@main @MainActor struct CFlowApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) private var delegate
    @StateObject private var model = FlowModel()
    init() { NSApplication.shared.setActivationPolicy(.accessory) }
    var body: some Scene {
        MenuBarExtra { MainPanel(model:model) } label: { Image(nsImage:Self.menuIcon).accessibilityLabel("CFlow") }
        .menuBarExtraStyle(.window)
        Settings { SettingsPanel(model:model) }
    }
    static var menuIcon: NSImage {
        let image=NSImage(size:NSSize(width:24,height:18),flipped:false) { _ in
            NSColor.labelColor.setStroke()
            let c=NSBezierPath();c.lineWidth=1.7;c.lineCapStyle = .round;c.lineJoinStyle = .round
            c.move(to:NSPoint(x:16,y:12));c.line(to:NSPoint(x:16,y:15));c.line(to:NSPoint(x:3,y:15));c.line(to:NSPoint(x:3,y:5));c.line(to:NSPoint(x:12,y:5));c.stroke()
            let small=NSBezierPath(roundedRect:NSRect(x:13,y:3,width:9,height:7),xRadius:1,yRadius:1);small.lineWidth=1.5;small.stroke()
            let base=NSBezierPath();base.lineWidth=1.5;base.move(to:NSPoint(x:12,y:2));base.line(to:NSPoint(x:23,y:2));base.move(to:NSPoint(x:7,y:5));base.line(to:NSPoint(x:7,y:2));base.move(to:NSPoint(x:4,y:2));base.line(to:NSPoint(x:10,y:2));base.stroke();return true
        };image.isTemplate=true;return image
    }
}

struct MainPanel: View {
    @ObservedObject var model:FlowModel
    @Environment(\.openSettings) private var openSettings
    @State private var initialized=false
    var body: some View {
        VStack(spacing:16) {
            HStack(spacing:10) {
                if let icon=NSImage(named:"CFlow") {Image(nsImage:icon).resizable().scaledToFit().frame(width:34,height:34)}
                Text("CFlow").font(.system(size:23,weight:.semibold));Spacer()
                Button {NSApplication.shared.activate(ignoringOtherApps:true);openSettings()} label:{Image(systemName:"gearshape").font(.system(size:18))}.buttonStyle(.plain).help("设置")
            }
            HStack {Image(systemName:"desktopcomputer").foregroundStyle(.blue);Image(systemName:"laptopcomputer").foregroundStyle(.blue);Text(model.windowsHost.isEmpty ? "未连接 Windows" : model.windowsHost).lineLimit(1);Spacer();Circle().fill(model.ready ? Color.green : Color.gray).frame(width:7,height:7)}.font(.system(size:12)).padding(12).background(.background,in:RoundedRectangle(cornerRadius:12))
            VStack(spacing:0) {
                modeToggle("扩展屏",symbol:"display",mode:"extend")
                Divider().padding(.leading,42)
                modeToggle("共用鼠标",symbol:"cursorarrow",mode:"deskflow")
            }.background(.background,in:RoundedRectangle(cornerRadius:12))
            VStack(spacing:0) {
                parameter("分辨率",symbol:"viewfinder") {
                    Picker("",selection:Binding(get:{"\(model.width)x\(model.height)"},set:{value in let p=value.split(separator:"x").compactMap{Int($0)};if p.count==2 {model.width=p[0];model.height=p[1];model.reconnectIfActive()}})) {Text("1920 × 1200").tag("1920x1200");Text("2560 × 1600").tag("2560x1600");Text("2880 × 1800").tag("2880x1800")}.labelsHidden().frame(width:150)
                }
                Divider().padding(.leading,42)
                parameter("帧率",symbol:"waveform.path") {Picker("",selection:Binding(get:{model.fps},set:{model.fps=$0;model.reconnectIfActive()})){ForEach([30,60],id:\.self){Text("\($0) FPS").tag($0)}}.labelsHidden().frame(width:120)}
                Divider().padding(.leading,42)
                parameter("码率",symbol:"speedometer") {Picker("",selection:Binding(get:{model.bitrate},set:{model.bitrate=$0;model.reconnectIfActive()})){ForEach([20,30,40,60,80],id:\.self){Text("\($0) Mbps").tag($0)}}.labelsHidden().frame(width:120)}
            }.background(.background,in:RoundedRectangle(cornerRadius:12)).disabled(model.busy||model.waiting)
            HStack {if model.busy||model.waiting {ProgressView().controlSize(.small)};Text(model.message).font(.caption).foregroundStyle(.secondary).lineLimit(2);Spacer();Menu {Button("设置"){openSettings()};Divider();Button("退出 CFlow"){Task{await model.quit()}}}label:{Image(systemName:"ellipsis")}.menuStyle(.borderlessButton).fixedSize()}
        }.padding(18).frame(width:354).background(Color(nsColor:.windowBackgroundColor))
        .task {if !initialized {initialized=true;if model.receiver==nil {model.start()}}}
        .alert("CFlow",isPresented:Binding(get:{!model.errorText.isEmpty},set:{if !$0 {model.errorText=""}})){Button("好"){model.errorText=""}}message:{Text(model.errorText)}
    }
    private func modeToggle(_ title:String,symbol:String,mode:String)->some View {
        Toggle(isOn:Binding(get:{model.mode==mode},set:{on in model.requestMode(on ? mode : "normal")})) {Label(title,systemImage:symbol).symbolRenderingMode(.hierarchical)}.toggleStyle(.switch).tint(.green).padding(12).disabled(model.busy||model.waiting||!model.ready)
    }
    private func parameter<Content:View>(_ title:String,symbol:String,@ViewBuilder content:()->Content)->some View {
        HStack {Image(systemName:symbol).foregroundStyle(.blue).frame(width:22);Text(title);Spacer();content()}.padding(12)
    }
}

struct SettingsPanel:View {
    @ObservedObject var model:FlowModel
    @State private var startup=false
    @State private var confirmReset=false
    @State private var confirmRevoke=false
    var body: some View {
        Form {
            Section("通用") {
                LabeledContent("外观",value:"跟随系统")
                Toggle("登录时启动 CFlow",isOn:Binding(get:{startup},set:{value in do{try model.setStartup(value);startup=model.autoStart}catch{model.errorText=error.localizedDescription}}))
                Text("仅启动菜单栏接收端，不自动开启模式。").font(.caption).foregroundStyle(.secondary)
                LabeledContent("关闭窗口",value:"收起到菜单栏")
            }
            Section("设备与连接") {
                LabeledContent("Windows 主机",value:model.windowsHost.isEmpty ? "尚未收到请求" : model.windowsHost)
                Button("复制双端控制配对码"){model.copyPairing()}.disabled(!model.ready)
                Button("使旧控制配对失效",role:.destructive){confirmRevoke=true}
                Text("粘贴到 Windows CFlow → 设置 → 双端控制配对。配对码含密钥，请勿公开。").font(.caption).foregroundStyle(.secondary)
                Button("打开 Moonlight 检查串流配对"){if let url=NSWorkspace.shared.urlForApplication(withBundleIdentifier:"com.moonlight-stream.Moonlight"){NSWorkspace.shared.open(url)}else{NSWorkspace.shared.open(URL(fileURLWithPath:"/Applications/Moonlight.app"))}}
                Text("Sunshine / Moonlight 配对与 CFlow 配对相互独立。Deskflow 沿用原客户端主机与系统权限配置。").font(.caption).foregroundStyle(.secondary)
            }
            Section("其他") {
                Toggle("保留 Windows 本机声音",isOn:$model.audioOnHost).onChange(of:model.audioOnHost){_ in model.save()}
                Text("这不会静音整个 Mac，Mac 的播放音量由 Moonlight 和系统决定。").font(.caption).foregroundStyle(.secondary)
                Button("查看本地诊断"){NSWorkspace.shared.open(FlowModel.logs)}
                Button("恢复默认设置",role:.destructive){confirmReset=true}
                Text("保留配对、证书、驱动和物理主屏布局。").font(.caption).foregroundStyle(.secondary)
            }
        }.formStyle(.grouped).frame(width:440,height:610).onAppear{startup=model.autoStart}
        .confirmationDialog("恢复工具偏好并关闭当前模式？",isPresented:$confirmReset,titleVisibility:.visible){Button("恢复默认",role:.destructive){Task{await model.reset();startup=model.autoStart}}}
        .confirmationDialog("关闭当前模式并使旧 Windows 控制配对失效？",isPresented:$confirmRevoke,titleVisibility:.visible){Button("解除控制配对",role:.destructive){Task{await model.revokePairing()}}}
        .alert("CFlow",isPresented:Binding(get:{!model.errorText.isEmpty},set:{if !$0{model.errorText=""}})){Button("好"){model.errorText=""}}message:{Text(model.errorText)}
    }
}
