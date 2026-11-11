import AppKit
import SwiftUI

@MainActor final class PanelNavigation: ObservableObject {
    @Published var showingSettings = false
}

@MainActor final class MenuBarController: NSObject {
    private let model: FlowModel
    private let navigation = PanelNavigation()
    private let item = NSStatusBar.system.statusItem(withLength: 28)
    private let popover = NSPopover()
    private let menu = NSMenu()

    init(model: FlowModel) {
        self.model = model
        super.init()

        popover.behavior = .transient
        popover.animates = true
        popover.contentSize = NSSize(width: 438, height: 500)
        popover.contentViewController = NSHostingController(rootView: MainPanel(model: model, navigation: navigation))

        if let button = item.button {
            button.image = Self.menuIcon
            button.imageScaling = .scaleProportionallyDown
            button.imagePosition = .imageOnly
            button.toolTip = "CFlow"
            button.target = self
            button.action = #selector(clicked(_:))
            button.sendAction(on: [.leftMouseUp, .rightMouseUp])
        }

        menu.autoenablesItems = false
        menu.addItem(NSMenuItem(title: "打开 CFlow", action: #selector(openMain), keyEquivalent: ""))
        menu.addItem(NSMenuItem(title: "设置", action: #selector(openSettings), keyEquivalent: ""))
        menu.addItem(.separator())
        menu.addItem(NSMenuItem(title: "退出 CFlow", action: #selector(quit), keyEquivalent: ""))
        for entry in menu.items { entry.target = self }
    }

    @objc private func clicked(_ sender: NSStatusBarButton) {
        let event = NSApp.currentEvent
        if event?.type == .rightMouseUp || event?.modifierFlags.contains(.control) == true {
            popover.performClose(nil)
            menu.popUp(positioning: nil, at: NSPoint(x: 0, y: sender.bounds.minY - 2), in: sender)
        } else if popover.isShown {
            popover.performClose(nil)
        } else {
            showPanel()
        }
    }

    func showPanel() {
        guard let button = item.button else { return }
        popover.show(relativeTo: button.bounds, of: button, preferredEdge: .minY)
        NSApp.activate(ignoringOtherApps: true)
        popover.contentViewController?.view.window?.makeKey()
    }

    @objc private func openMain() {
        navigation.showingSettings = false
        showPanel()
    }

    @objc private func openSettings() {
        navigation.showingSettings = true
        showPanel()
    }

    @objc private func quit() {
        popover.performClose(nil)
        Task { await model.quit() }
    }

    private static var menuIcon: NSImage {
        let image = NSImage(size: NSSize(width: 24, height: 18), flipped: false) { _ in
            NSColor.labelColor.setStroke()
            let large = NSBezierPath()
            large.lineWidth = 1.7
            large.lineCapStyle = .round
            large.lineJoinStyle = .round
            large.move(to: NSPoint(x: 16, y: 12))
            large.line(to: NSPoint(x: 16, y: 15))
            large.line(to: NSPoint(x: 3, y: 15))
            large.line(to: NSPoint(x: 3, y: 5))
            large.line(to: NSPoint(x: 12, y: 5))
            large.stroke()

            let small = NSBezierPath(roundedRect: NSRect(x: 13, y: 3, width: 9, height: 7), xRadius: 1, yRadius: 1)
            small.lineWidth = 1.5
            small.stroke()
            let base = NSBezierPath()
            base.lineWidth = 1.5
            base.move(to: NSPoint(x: 12, y: 2))
            base.line(to: NSPoint(x: 23, y: 2))
            base.move(to: NSPoint(x: 7, y: 5))
            base.line(to: NSPoint(x: 7, y: 2))
            base.move(to: NSPoint(x: 4, y: 2))
            base.line(to: NSPoint(x: 10, y: 2))
            base.stroke()
            return true
        }
        image.isTemplate = true
        return image
    }
}
