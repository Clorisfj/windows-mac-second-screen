import Foundation
import CryptoKit
import Combine

@MainActor final class PairingGate: ObservableObject {
    @Published private(set) var accepting = false
    @Published private(set) var code = ""
    @Published private(set) var host = ""
    @Published private(set) var requestPending = false
    @Published private(set) var message = "在 Windows 输入这台 Mac 的 IP 发起配对。"
    private var openUntil = Date.distantPast
    private var pending: Pending?

    private struct Pending {
        let id: String
        let host: String
        let commitment: String
        let serverNonce: String
        let expires: Date
        var clientNonce = ""
        var approved = false
    }

    static func commitment(_ nonce: String) -> String {
        SHA256.hash(data: Data(nonce.utf8)).map { String(format: "%02X", $0) }.joined()
    }

    static func verificationCode(_ fingerprint: String, _ clientNonce: String, _ serverNonce: String) -> String {
        let digest = Array(SHA256.hash(data: Data((fingerprint.uppercased() + "|" + clientNonce + "|" + serverNonce).utf8)))
        let value = (UInt32(digest[0]) << 24) | (UInt32(digest[1]) << 16) | (UInt32(digest[2]) << 8) | UInt32(digest[3])
        return String(format: "%06u", value % 1_000_000)
    }

    func enable() {
        pending = nil; code = ""; host = ""; accepting = true; requestPending = false
        openUntil = Date().addingTimeInterval(120)
        let expires = openUntil
        message = "两分钟内，在 Windows 输入本机 IP 并点击搜索配对。"
        Task {
            try? await Task.sleep(nanoseconds: 120_000_000_000)
            if openUntil == expires && pending == nil { accepting = false; message = "配对窗口已关闭。" }
        }
    }

    func cancel() {
        accepting = false; openUntil = .distantPast; pending = nil; code = ""; host = ""; requestPending = false
        message = "配对已关闭。"
    }

    func approve() {
        guard var value = pending, !value.clientNonce.isEmpty, value.expires > Date() else { cancel(); return }
        value.approved = true; pending = value; code = ""
        message = "已允许 Windows，正在保存连接。"
    }

    func finish(host remote: String) {
        guard let value = pending, value.approved, value.host == remote else { return }
        cancel(); message = "已完成配对。"
    }

    func handle(_ request: [String: Any], host remote: String, fingerprint: String, token: String) throws -> [String: Any] {
        let action = request["action"] as? String ?? ""
        if action == "pair.begin" {
            guard accepting && openUntil > Date(), pending == nil,
                  let commitment = request["clientCommit"] as? String,
                  commitment.range(of: "^[0-9A-F]{64}$", options: .regularExpression) != nil else {
                throw FlowError.message("请先在 Mac CFlow 设置中开启“允许配对”，已有请求请先拒绝或等待到期。")
            }
            let value = Pending(id: UUID().uuidString.replacingOccurrences(of: "-", with: ""), host: remote,
                                commitment: commitment, serverNonce: try Vault.random(), expires: Date().addingTimeInterval(120))
            pending = value; accepting = false; host = remote; requestPending = true
            message = "正在验证 Windows 配对请求…"
            let id = value.id
            Task {
                try? await Task.sleep(nanoseconds: 120_000_000_000)
                if pending?.id == id { cancel(); message = "配对请求已到期。" }
            }
            return ["ok": true, "session": value.id, "serverNonce": value.serverNonce, "name": Host.current().localizedName ?? "Mac"]
        }

        guard var value = pending, value.expires > Date(), value.host == remote,
              request["session"] as? String == value.id,
              let nonce = request["clientNonce"] as? String,
              Data(base64Encoded: nonce)?.count == 32,
              Self.commitment(nonce) == value.commitment else {
            throw FlowError.message("此次配对已取消或过期，请重新发起。")
        }
        if action == "pair.verify" {
            if value.clientNonce.isEmpty {
                value.clientNonce = nonce; pending = value
                code = Self.verificationCode(fingerprint, nonce, value.serverNonce)
                message = "确认 Windows 也显示相同数字，再点允许。"
            }
            return ["ok": true]
        }
        guard action == "pair.finish", value.clientNonce == nonce else { throw FlowError.message("配对步骤无效。") }
        if !value.approved { return ["ok": true, "state": "pending"] }
        return ["ok": true, "state": "approved", "token": token]
    }
}
