import Foundation
import AppKit
import SwiftUI
import ServiceManagement
import Darwin

@MainActor final class FlowModel: ObservableObject {
    static weak var instance: FlowModel?
    @Published var mode = "normal"
    @Published var busy = false
    @Published var message = "正在准备接收端…"
    @Published var windowsHost: String = UserDefaults.standard.string(forKey: "windowsHost") ?? ""
    @Published var width = UserDefaults.standard.integer(forKey: "width") == 0 ? 2880 : UserDefaults.standard.integer(forKey: "width")
    @Published var height = UserDefaults.standard.integer(forKey: "height") == 0 ? 1800 : UserDefaults.standard.integer(forKey: "height")
    @Published var fps = UserDefaults.standard.integer(forKey: "fps") == 0 ? 60 : UserDefaults.standard.integer(forKey: "fps")
    @Published var bitrate = UserDefaults.standard.integer(forKey: "bitrate") == 0 ? 40 : UserDefaults.standard.integer(forKey: "bitrate")
    @Published var audioOnHost = UserDefaults.standard.object(forKey: "audioOnHost") as? Bool ?? true
    @Published var pairing = ""
    @Published var errorText = ""
    @Published var ready = false
    @Published var waiting = false
    private var pendingMode = ""
    private var pendingID = ""
    var receiver: Receiver?
    private var identity: Identity?
    private var child: Process?
    private var generation = UUID()
    private var logTail = ""
    var autoStart: Bool { SMAppService.mainApp.status == .enabled }
    static var logs:URL {Identity.folder.appendingPathComponent("Logs",isDirectory:true)}
    static func record(_ text:String) {try? FileManager.default.createDirectory(at:logs,withIntermediateDirectories:true);let path=logs.appendingPathComponent("control.log");let data=Data((ISO8601DateFormatter().string(from:Date())+" "+text+"\n").utf8);if !FileManager.default.fileExists(atPath:path.path){FileManager.default.createFile(atPath:path.path,contents:data)}else if let handle=try? FileHandle(forWritingTo:path){handle.seekToEndOfFile();handle.write(data);try? handle.close()}}
    init() { Self.instance=self; Task { @MainActor in self.start() } }
    func start() {
        if receiver != nil { return }
        Self.record("接收端启动")
        do {
            let value = try Identity(); identity = value
            receiver = try Receiver(identity: value) { [weak self] request, host, reply in
                Task { @MainActor in
                    guard let self = self else { reply(["ok": false, "error": "接收端已退出"]); return }
                    do {
                        guard request["token"] as? String == Vault.read("pair-token") else {throw FlowError.message("控制配对已失效。")}
                        self.windowsHost = host; UserDefaults.standard.set(host, forKey: "windowsHost")
                        let action = request["action"] as? String ?? ""
                        if action != "status" {
                            guard !self.busy else { throw FlowError.message("Mac 正在切换模式，请稍候。") }
                            if action != "stop" { try self.apply(request) }
                            try await self.change(action == "stop" ? "normal" : action)
                        }
                        reply(["ok": true, "mode": self.mode, "message": self.message, "requestedMode": self.pendingMode, "requestID": self.pendingID, "width": self.width, "height": self.height, "fps": self.fps, "bitrate": self.bitrate, "audioOnHost": self.audioOnHost])
                    } catch { self.errorText = error.localizedDescription;Self.record("操作未完成："+error.localizedDescription);reply(["ok": false, "error": error.localizedDescription]) }
                }
            }
            receiver?.onState = { [weak self] state,text in Task { @MainActor in self?.message = text;self?.ready=state;Self.record(text) } }
            receiver?.start(); pairing = try value.pairingCode()
        } catch { errorText = error.localizedDescription; message = "接收端未启动" }
    }
    func save() {
        for (key,value) in ["width":width,"height":height,"fps":fps,"bitrate":bitrate] { UserDefaults.standard.set(value, forKey:key) }
        UserDefaults.standard.set(audioOnHost, forKey:"audioOnHost")
    }
    private func apply(_ body: [String: Any]) throws {
        guard let w = body["width"] as? Int, let h = body["height"] as? Int, let f = body["fps"] as? Int, let b = body["bitrate"] as? Int,
              [(1920,1200),(2560,1600),(2880,1800)].contains(where: {$0.0 == w && $0.1 == h}), [30,60].contains(f), (10...80).contains(b) else { throw FlowError.message("串流参数无效。") }
        width=w; height=h; fps=f; bitrate=b; audioOnHost=body["audioOnHost"] as? Bool ?? true; save()
    }
    private func app(_ name: String) throws -> URL {
        let paths = ["/Applications/\(name).app", FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Applications/\(name).app").path]
        guard let path=paths.first(where: {FileManager.default.fileExists(atPath:$0)}) else { throw FlowError.message("没有找到 \(name).app，请先从官方来源安装。") }
        return URL(fileURLWithPath:path)
    }
    private func binary(_ name: String, core: Bool = false) throws -> URL {
        let bundle = try app(name)
        if !core, let executable=Bundle(url:bundle)?.executableURL { return executable }
        let candidates = ["Contents/MacOS/deskflow-core", "Contents/Resources/deskflow-core", "Contents/bin/deskflow-core"]
        guard let file=candidates.map({bundle.appendingPathComponent($0)}).first(where:{FileManager.default.isExecutableFile(atPath:$0.path)}) else { throw FlowError.message("Deskflow 包内未找到客户端核心。请确认原版 Deskflow 已配置为客户端。") }
        return file
    }
    private func stopApplications() async throws {
        generation = UUID()
        let previous = child
        if let previous = previous, previous.isRunning { previous.terminate() }
        child = nil
        for name in ["Moonlight","Deskflow"] {
            guard let url = try? app(name) else { continue }
            for application in NSWorkspace.shared.runningApplications where application.bundleURL?.standardizedFileURL == url.standardizedFileURL { application.terminate() }
        }
        for _ in 0..<10 {
            let running = ["Moonlight","Deskflow"].contains { name in
                guard let url = try? app(name) else {return false}
                return NSWorkspace.shared.runningApplications.contains { $0.bundleURL?.standardizedFileURL == url.standardizedFileURL && !$0.isTerminated }
            }
            if !running && previous?.isRunning != true { break }; try? await Task.sleep(nanoseconds: 200_000_000)
        }
        if let previous = previous, previous.isRunning { _ = Darwin.kill(previous.processIdentifier, SIGKILL) }
        for name in ["Moonlight","Deskflow"] {
            guard let url = try? app(name) else {continue}
            for application in NSWorkspace.shared.runningApplications where application.bundleURL?.standardizedFileURL == url.standardizedFileURL && !application.isTerminated {
                guard application.forceTerminate() else {throw FlowError.message("无法停止 \(name)，请在原应用退出后重试。")}
            }
        }
    }
    func change(_ next: String) async throws {
        guard ["normal","extend","deskflow"].contains(next) else { throw FlowError.message("模式无效。") }
        guard !busy else {throw FlowError.message("正在切换模式，请稍候。")}
        waiting=false;pendingMode="";pendingID=""
        busy=true; defer {busy=false}
        try await stopApplications(); mode="normal"
        if next == "normal" {message="普通模式";return}
        guard privateIPv4(windowsHost) else {throw FlowError.message("先在 Windows CFlow 输入本机配对码。Windows 发来请求后会记录主机地址。")}
        let process=Process()
        if next == "extend" {
            process.executableURL=try binary("Moonlight")
            process.arguments=["stream",windowsHost,"Desktop","--resolution","\(width)x\(height)","--fps","\(fps)","--bitrate","\(bitrate * 1000)","--video-codec","HEVC","--display-mode","fullscreen",audioOnHost ? "--audio-on-host" : "--no-audio-on-host"]
        } else {
            process.executableURL=try binary("Deskflow",core:true);process.arguments=["client"]
        }
        logTail="";let output=Pipe();process.standardOutput=output;process.standardError=output
        let current=generation
        output.fileHandleForReading.readabilityHandler={ [weak self] handle in
            let data=handle.availableData;if data.isEmpty {handle.readabilityHandler=nil;return}
            let text=String(decoding:data,as:UTF8.self)
            Task { @MainActor in self?.logTail=String(((self?.logTail ?? "")+text).suffix(8000)) }
        }
        process.terminationHandler={ [weak self] p in Task { @MainActor in
            guard let self=self,self.generation==current else{return};self.mode="normal";self.message="客户端已结束";if p.terminationStatus != 0 {self.errorText="客户端返回 \(p.terminationStatus)：\(String(self.logTail.suffix(500)))"}
        }}
        try process.run();child=process
        try await Task.sleep(nanoseconds:1_500_000_000)
        guard process.isRunning else {throw FlowError.message("客户端未保持运行。请先完成原应用配对并检查其版本。\n\(String(logTail.suffix(500)))")}
        mode=next;message=next == "extend" ? "Moonlight 已启动，画面待确认" : "Deskflow 已启动，连接待确认"
    }
    func setStartup(_ enabled: Bool) throws { if enabled {try SMAppService.mainApp.register()} else {try SMAppService.mainApp.unregister()} ; objectWillChange.send() }
    func requestMode(_ next:String) {
        guard !busy && !waiting else {return}
        guard !windowsHost.isEmpty else {errorText="请先在 Windows CFlow 完成配对，并保持 Windows CFlow 运行。";return}
        pendingMode=next;pendingID=UUID().uuidString;let id=pendingID;waiting=true;message="等待 Windows 主控确认…"
        Task{try? await Task.sleep(nanoseconds:90_000_000_000);if pendingID==id {pendingMode="";pendingID="";waiting=false;errorText="Windows 尚未完成请求，请检查 Windows CFlow 和管理员授权。"}}
    }
    func reconnectIfActive() {save();requestMode(mode)}
    func copyPairing() {do {if let identity=identity {pairing=try identity.pairingCode()};NSPasteboard.general.clearContents();NSPasteboard.general.setString(pairing,forType:.string)}catch{errorText=error.localizedDescription}}
    func revokePairing() async {
        guard !busy else {return}
        do {try await change("normal");try Vault.write("pair-token",Vault.random());receiver?.stop();receiver=nil;identity=nil;ready=false;windowsHost="";UserDefaults.standard.removeObject(forKey:"windowsHost");try await Task.sleep(nanoseconds:500_000_000);start();message="旧配对已失效，请复制新配对码"}catch{errorText=error.localizedDescription}
    }
    func reset() async {
        do {try await change("normal");if autoStart {try setStartup(false)};width=2880;height=1800;fps=60;bitrate=40;audioOnHost=true;save();message="已恢复偏好，配对与驱动保持"}catch{errorText=error.localizedDescription}
    }
    func quit() async {try? await change("normal");receiver?.stop();NSApplication.shared.terminate(nil)}
    func emergencyStop() {generation=UUID();if let child=child,child.isRunning {child.terminate()};receiver?.stop()}
}
