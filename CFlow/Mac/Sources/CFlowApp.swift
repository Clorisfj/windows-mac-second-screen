import SwiftUI
import AppKit

@main @MainActor struct CFlowApp {
    static func main() {
        let application = NSApplication.shared
        let delegate = AppDelegate()
        application.setActivationPolicy(.accessory)
        application.delegate = delegate
        withExtendedLifetime(delegate) { application.run() }
    }
}

@MainActor final class AppDelegate: NSObject, NSApplicationDelegate {
    private let model = FlowModel()
    private var menuBar: MenuBarController?

    func applicationDidFinishLaunching(_ notification: Notification) {
        menuBar = MenuBarController(model: model)
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        menuBar?.showPanel()
        return true
    }

    func applicationWillTerminate(_ notification: Notification) {
        model.emergencyStop()
    }
}

private enum PanelColors {
    static let background = Color(nsColor: NSColor(name: nil) { appearance in
        if appearance.bestMatch(from: [.aqua, .darkAqua]) == .darkAqua {
            return NSColor(srgbRed: 24.0/255, green: 24.0/255, blue: 26.0/255, alpha: 1)
        }
        return NSColor(srgbRed: 242.0/255, green: 242.0/255, blue: 247.0/255, alpha: 1)
    })
    static let card = Color(nsColor: NSColor(name: nil) { appearance in
        if appearance.bestMatch(from: [.aqua, .darkAqua]) == .darkAqua {
            return NSColor(srgbRed: 39.0/255, green: 39.0/255, blue: 42.0/255, alpha: 1)
        }
        return .white
    })
}

private struct PanelCard<Content: View>: View {
    let content: Content
    init(@ViewBuilder content: () -> Content) { self.content = content() }
    var body: some View {
        VStack(spacing: 0) { content }
            .background(PanelColors.card, in: RoundedRectangle(cornerRadius: 13))
    }
}

struct MainPanel: View {
    @ObservedObject var model: FlowModel
    @ObservedObject var navigation: PanelNavigation

    var body: some View {
        Group {
            if navigation.showingSettings {
                SettingsPanel(model: model, goBack: { navigation.showingSettings = false })
            } else {
                dashboard
            }
        }
        .frame(width: 438, height: 500)
        .font(.system(size: 14))
        .background(PanelColors.background)
        .tint(.blue)
        .alert("CFlow", isPresented: Binding(get: { !model.errorText.isEmpty }, set: { if !$0 { model.errorText = "" } })) {
            Button("好") { model.errorText = "" }
        } message: { Text(model.errorText) }
    }

    private var dashboard: some View {
        VStack(spacing: 16) {
            HStack(spacing: 10) {
                if let image = NSImage(named: "CFlow") {
                    Image(nsImage: image).resizable().scaledToFit().frame(width: 40, height: 40)
                }
                Text("CFlow").font(.system(size: 28, weight: .semibold))
                Spacer()
                Button { navigation.showingSettings = true } label: {
                    Image(systemName: "gearshape").font(.system(size: 19)).frame(width: 40, height: 36)
                        .background(PanelColors.card, in: RoundedRectangle(cornerRadius: 6))
                }.buttonStyle(.plain).help("设置")
            }.frame(height: 40)

            PanelCard {
                HStack(spacing: 10) {
                    Image(systemName: "desktopcomputer").foregroundStyle(.blue)
                    Image(systemName: "laptopcomputer").foregroundStyle(.blue)
                    Text(model.connected ? "\(model.windowsHost) · 已连接" : "Windows 未连接")
                        .foregroundStyle(model.connected ? .primary : .secondary).lineLimit(1)
                    Spacer(minLength: 0)
                }.padding(.horizontal, 16).frame(height: 53)
            }

            PanelCard {
                modeToggle("扩展屏", symbol: "display", mode: "extend")
                Divider().padding(.horizontal, 16)
                modeToggle("共用鼠标", symbol: "cursorarrow", mode: "deskflow")
            }

            PanelCard {
                parameter("分辨率", symbol: "viewfinder", value: "\(model.width) × \(model.height)") {
                    Button("1920 × 1200") { setResolution(1920, 1200) }
                    Button("2560 × 1600") { setResolution(2560, 1600) }
                    Button("2880 × 1800") { setResolution(2880, 1800) }
                }
                Divider().padding(.horizontal, 16)
                parameter("帧率", symbol: "waveform.path", value: "\(model.fps) FPS") {
                    ForEach([30, 60], id: \.self) { value in
                        Button("\(value) FPS") { model.fps = value; model.reconnectIfActive() }
                    }
                }
                Divider().padding(.horizontal, 16)
                parameter("码率", symbol: "speedometer", value: "\(model.bitrate) Mbps") {
                    ForEach([20, 30, 40, 60, 80], id: \.self) { value in
                        Button("\(value) Mbps") { model.bitrate = value; model.reconnectIfActive() }
                    }
                }
            }.disabled(model.busy || model.waiting)

            HStack(spacing: 8) {
                if model.busy || model.waiting { ProgressView().controlSize(.small) }
                Text(model.message).font(.system(size: 12)).foregroundStyle(.secondary).lineLimit(2)
                Spacer(minLength: 0)
            }.frame(height: 38, alignment: .topLeading)
        }.padding(22)
    }

    private func modeToggle(_ title: String, symbol: String, mode: String) -> some View {
        HStack(spacing: 12) {
            Image(systemName: symbol).foregroundStyle(.blue).frame(width: 24)
            Text(title)
            Spacer()
            Toggle(title, isOn: Binding(get: { model.mode == mode }, set: { model.requestMode($0 ? mode : "normal") }))
                .labelsHidden().toggleStyle(.switch).tint(.green)
        }
        .padding(.horizontal, 16).frame(height: 53)
        .disabled(model.busy || model.waiting || !model.connected)
    }

    private func parameter<Choices: View>(_ title: String, symbol: String, value: String, @ViewBuilder choices: () -> Choices) -> some View {
        HStack(spacing: 12) {
            Image(systemName: symbol).foregroundStyle(.blue).frame(width: 24)
            Text(title)
            Spacer()
            Menu(content: choices) {
                HStack(spacing: 12) {
                    Text(value).foregroundStyle(.primary)
                    Image(systemName: "chevron.right").font(.system(size: 10)).foregroundStyle(.secondary)
                }
            }.menuStyle(.borderlessButton).menuIndicator(.hidden).fixedSize()
        }.padding(.horizontal, 16).frame(height: 50)
    }

    private func setResolution(_ width: Int, _ height: Int) {
        model.width = width; model.height = height; model.reconnectIfActive()
    }
}

private enum SettingsPage {
    case home, pairing, deskflow, moonlight
    var title: String {
        switch self {
        case .home: return "设置"
        case .pairing: return "连接 Windows"
        case .deskflow: return "Deskflow 连接"
        case .moonlight: return "串流配对"
        }
    }
}

private struct SettingsPanel: View {
    @ObservedObject var model: FlowModel
    let goBack: () -> Void
    @State private var page = SettingsPage.home
    @State private var startup = false
    @State private var confirmReset = false
    @State private var confirmRevoke = false

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(spacing: 16) {
                Button {
                    if page == .home { goBack() } else { page = .home }
                } label: {
                    Label("返回", systemImage: "chevron.left").frame(width: 88, height: 36)
                        .background(PanelColors.card, in: RoundedRectangle(cornerRadius: 6))
                }.buttonStyle(.plain)
                Text(page.title).font(.system(size: 22, weight: .semibold))
                Spacer(minLength: 0)
            }.frame(height: 40)

            ScrollView {
                VStack(alignment: .leading, spacing: 18) {
                    switch page {
                    case .home: home
                    case .pairing: pairing
                    case .deskflow: deskflow
                    case .moonlight: moonlight
                    }
                }.padding(.bottom, 4)
            }
        }
        .padding(22)
        .onAppear { startup = model.autoStart }
        .confirmationDialog("恢复工具偏好并关闭当前模式？", isPresented: $confirmReset, titleVisibility: .visible) {
            Button("恢复默认", role: .destructive) { Task { await model.reset(); startup = model.autoStart } }
        }
        .confirmationDialog("关闭当前模式并使旧 Windows 控制配对失效？", isPresented: $confirmRevoke, titleVisibility: .visible) {
            Button("解除控制配对", role: .destructive) { Task { await model.revokePairing() } }
        }
    }

    private var home: some View {
        Group {
            section("通用") {
                settingsToggle("开机启动 CFlow", isOn: Binding(get: { startup }, set: { value in
                    do { try model.setStartup(value); startup = model.autoStart }
                    catch { model.errorText = error.localizedDescription }
                }))
            }
            section("设备与连接") {
                row("双端控制配对", value: model.connected ? "已连接" : "查看 IP") { page = .pairing }
                Divider().padding(.horizontal, 16)
                row("Moonlight 串流配对", value: "查看步骤") { page = .moonlight }
                Divider().padding(.horizontal, 16)
                row("Deskflow 连接", value: "地址 / 状态") { page = .deskflow }
            }
            section("其他") {
                settingsToggle("保留 Windows 本机声音", isOn: Binding(get: { model.audioOnHost }, set: { model.audioOnHost = $0; model.save() }))
                Divider().padding(.horizontal, 16)
                row("诊断与日志", value: "本地查看") { NSWorkspace.shared.open(FlowModel.logs) }
                Divider().padding(.horizontal, 16)
                row("恢复默认设置", value: "保留配对", destructive: true) { confirmReset = true }
                    .disabled(model.busy || model.waiting)
            }
            Text("开机启动仅打开 CFlow，功能开关由你控制。").font(.system(size: 12)).foregroundStyle(.secondary)
        }
    }

    private var pairing: some View {
        Group {
            PanelCard { PairingSettings(model: model, gate: model.pairingGate).padding(16) }
            PanelCard {
                row("使旧控制配对失效", value: "", destructive: true) { confirmRevoke = true }
                    .disabled(model.busy || model.waiting)
            }
        }
    }

    private var deskflow: some View {
        Group {
            PanelCard {
                LabeledContent("Windows IP", value: model.windowsHost.isEmpty ? "尚未配对" : model.windowsHost).padding(16)
                Divider().padding(.horizontal, 16)
                LabeledContent("连接", value: model.connected ? "Windows CFlow 在线" : "Windows CFlow 未连接").padding(16)
            }
            Text("返回首页开启共用鼠标。Deskflow 沿用现有客户端配置与辅助功能权限，客户端中的服务器地址应与上方 Windows IP 一致。")
                .font(.system(size: 12)).foregroundStyle(.secondary)
        }
    }

    private var moonlight: some View {
        Group {
            Text("已能串流时可直接使用现有配对。").font(.system(size: 12)).foregroundStyle(.secondary)
            PanelCard {
                LabeledContent("Windows IP", value: model.windowsHost.isEmpty ? "尚未配对" : model.windowsHost).padding(16)
                Divider().padding(.horizontal, 16)
                row("打开 Moonlight", value: "检查串流配对") {
                    let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: "com.moonlight-stream.Moonlight") ?? URL(fileURLWithPath: "/Applications/Moonlight.app")
                    if !NSWorkspace.shared.open(url) { model.errorText = "无法打开 Moonlight，请检查安装位置。" }
                }
            }
            Text("首次使用时在 Moonlight 添加 Windows IP，再在 Windows Sunshine 管理页确认 PIN。").font(.system(size: 12)).foregroundStyle(.secondary)
        }
    }

    private func section<Content: View>(_ title: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(title).font(.system(size: 12)).foregroundStyle(.secondary).padding(.leading, 8)
            PanelCard(content: content)
        }
    }

    private func settingsToggle(_ title: String, isOn: Binding<Bool>) -> some View {
        Toggle(title, isOn: isOn).toggleStyle(.switch).tint(.green).padding(.horizontal, 16).frame(height: 50)
    }

    private func row(_ title: String, value: String, destructive: Bool = false, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            HStack(spacing: 12) {
                Text(title).foregroundColor(destructive ? .red : .primary)
                Spacer(minLength: 8)
                Text(value).foregroundStyle(.secondary)
                Image(systemName: "chevron.right").font(.system(size: 10)).foregroundStyle(.secondary)
            }.padding(.horizontal, 16).frame(height: 50).contentShape(Rectangle())
        }.buttonStyle(.plain)
    }
}

private struct PairingSettings: View {
    @ObservedObject var model: FlowModel
    @ObservedObject var gate: PairingGate
    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            LabeledContent("Mac IP", value: Identity.address() ?? "无可用局域网地址").textSelection(.enabled)
            LabeledContent("Windows", value: model.connected ? model.windowsHost : "未连接")
            Divider()
            if !gate.code.isEmpty {
                Text(gate.code).font(.system(size: 30, weight: .medium, design: .monospaced)).frame(maxWidth: .infinity)
                Text("确认 Windows 显示相同数字").font(.system(size: 12))
                Text("请求来自 \(gate.host)").font(.system(size: 12)).foregroundStyle(.secondary)
                HStack {
                    Button("拒绝", role: .destructive) { gate.cancel() }
                    Spacer()
                    Button("允许配对") { gate.approve() }.buttonStyle(.borderedProminent)
                }
            } else if gate.requestPending {
                Button("取消配对") { gate.cancel() }
            } else {
                Button(gate.accepting ? "关闭配对" : "允许配对（两分钟）") {
                    if gate.accepting { gate.cancel() } else { gate.enable() }
                }.disabled(!model.ready)
            }
            Text(gate.message).font(.system(size: 12)).foregroundStyle(.secondary)
        }
    }
}
