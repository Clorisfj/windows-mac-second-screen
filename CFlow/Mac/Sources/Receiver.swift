import Foundation
import Network
import Security
import CryptoKit
import Darwin

enum FlowError: LocalizedError {
    case message(String)
    var errorDescription: String? { if case .message(let text) = self { return text }; return nil }
}

enum Vault {
    static func read(_ name: String) -> String? {
        let query: [String: Any] = [kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: "io.github.clorisfj.cflow", kSecAttrAccount as String: name, kSecReturnData as String: true, kSecMatchLimit as String: kSecMatchLimitOne]
        var result: CFTypeRef?
        guard SecItemCopyMatching(query as CFDictionary, &result) == errSecSuccess, let data = result as? Data else { return nil }
        return String(data: data, encoding: .utf8)
    }
    static func write(_ name: String, _ value: String) throws {
        let query: [String: Any] = [kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: "io.github.clorisfj.cflow", kSecAttrAccount as String: name]
        let data = Data(value.utf8)
        let status = SecItemUpdate(query as CFDictionary, [kSecValueData as String: data] as CFDictionary)
        if status == errSecItemNotFound {
            var item = query; item[kSecValueData as String] = data
            item[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
            guard SecItemAdd(item as CFDictionary, nil) == errSecSuccess else { throw FlowError.message("无法保存钥匙串项目。") }
        } else if status != errSecSuccess { throw FlowError.message("钥匙串拒绝更新。") }
    }
    static func random() throws -> String {
        var bytes = [UInt8](repeating: 0, count: 32)
        guard SecRandomCopyBytes(kSecRandomDefault, bytes.count, &bytes) == errSecSuccess else { throw FlowError.message("无法生成配对密钥。") }
        return Data(bytes).base64EncodedString()
    }
}

final class Identity {
    let identity: SecIdentity
    let fingerprint: String
    let token: String
    static let folder = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("CFlow", isDirectory: true)
    init() throws {
        try FileManager.default.createDirectory(at: Self.folder, withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700])
        if let saved = Vault.read("pair-token") { token = saved } else { let value = try Vault.random(); try Vault.write("pair-token", value); token = value }
        let p12 = Self.folder.appendingPathComponent("receiver.p12")
        var password = Vault.read("identity-password")
        if !FileManager.default.fileExists(atPath: p12.path) || password == nil {
            password = try Vault.random(); try Vault.write("identity-password", password!)
            let key = Self.folder.appendingPathComponent("private.pem"), cert = Self.folder.appendingPathComponent("certificate.pem")
            defer { try? FileManager.default.removeItem(at: key); try? FileManager.default.removeItem(at: cert) }
            try Self.run(["req", "-x509", "-newkey", "rsa:2048", "-nodes", "-days", "3650", "-subj", "/CN=CFlow", "-keyout", key.path, "-out", cert.path])
            try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: key.path)
            try Self.run(["pkcs12", "-export", "-inkey", key.path, "-in", cert.path, "-out", p12.path, "-passout", "stdin"], input: password! + "\n")
            try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: p12.path)
        }
        var items: CFArray?
        let status = SecPKCS12Import(try Data(contentsOf: p12) as CFData, [kSecImportExportPassphrase as String: password!] as CFDictionary, &items)
        guard status == errSecSuccess, let list = items as? [[String: Any]], let first = list.first, let imported = first[kSecImportItemIdentity as String] else { throw FlowError.message("无法载入控制证书。请查看本地日志，不要关闭系统证书保护。") }
        identity = imported as! SecIdentity
        var cert: SecCertificate?
        guard SecIdentityCopyCertificate(identity, &cert) == errSecSuccess, let certificate = cert else { throw FlowError.message("证书无效。") }
        fingerprint = SHA256.hash(data: SecCertificateCopyData(certificate) as Data).map { String(format: "%02X", $0) }.joined()
    }
    static func run(_ args: [String], input: String? = nil) throws {
        let process = Process(); process.executableURL = URL(fileURLWithPath: "/usr/bin/openssl"); process.arguments = args
        process.standardOutput = FileHandle.nullDevice; process.standardError = FileHandle.nullDevice
        let pipe = Pipe(); if input != nil { process.standardInput = pipe }
        try process.run()
        if let text = input { pipe.fileHandleForWriting.write(Data(text.utf8)); try? pipe.fileHandleForWriting.close() }
        process.waitUntilExit(); if process.terminationStatus != 0 { throw FlowError.message("无法创建本机 TLS 证书，openssl 返回 \(process.terminationStatus)。") }
    }
    static func address() -> String? {
        var ptr: UnsafeMutablePointer<ifaddrs>?
        guard getifaddrs(&ptr) == 0 else { return nil }; defer { freeifaddrs(ptr) }
        var current = ptr
        while let item = current {
            defer { current = item.pointee.ifa_next }
            guard let addr = item.pointee.ifa_addr, addr.pointee.sa_family == UInt8(AF_INET), (item.pointee.ifa_flags & UInt32(IFF_UP)) != 0 else { continue }
            var host = [CChar](repeating: 0, count: Int(NI_MAXHOST))
            guard getnameinfo(addr, socklen_t(addr.pointee.sa_len), &host, socklen_t(host.count), nil, 0, NI_NUMERICHOST) == 0 else { continue }
            let value = String(cString: host); if privateIPv4(value) { return value }
        }
        return nil
    }

}

func privateIPv4(_ text: String) -> Bool {
    let segments = text.split(separator: ".", omittingEmptySubsequences: false)
    guard segments.count == 4 else { return false }
    let parts = segments.compactMap { Int($0) }
    guard parts.count == 4, parts.allSatisfy({ (0...255).contains($0) }) else { return false }
    return parts[0] == 10 || (parts[0] == 172 && (16...31).contains(parts[1])) || (parts[0] == 192 && parts[1] == 168)
}

final class Receiver {
    private let listener: NWListener
    private let queue = DispatchQueue(label: "CFlow.receiver")
    private let identity: Identity
    private let handle: ([String: Any], String, @escaping ([String: Any]) -> Void) -> Void
    var onState: ((Bool, String) -> Void)?
    init(identity: Identity, handle: @escaping ([String: Any], String, @escaping ([String: Any]) -> Void) -> Void) throws {
        self.identity = identity; self.handle = handle
        let tls = NWProtocolTLS.Options()
        guard let local = sec_identity_create(identity.identity) else { throw FlowError.message("无法启用 TLS。") }
        sec_protocol_options_set_local_identity(tls.securityProtocolOptions, local)
        sec_protocol_options_set_min_tls_protocol_version(tls.securityProtocolOptions, .TLSv12)
        let params = NWParameters(tls: tls, tcp: NWProtocolTCP.Options()); params.allowLocalEndpointReuse = true
        listener = try NWListener(using: params, on: 47999)
        listener.newConnectionHandler = { [weak self] connection in self?.accept(connection) }
        listener.stateUpdateHandler = { [weak self] state in
            switch state { case .ready: self?.onState?(true,"等待 Windows 连接"); case .failed(let error): self?.onState?(false,"接收端不可用：\(error.localizedDescription)"); default: break }
        }
    }
    func start() { listener.start(queue: queue) }
    func stop() { listener.cancel() }
    private func accept(_ connection: NWConnection) {
        guard case .hostPort(let host, _) = connection.endpoint else { connection.cancel(); return }
        let remote = String(describing: host)
        guard privateIPv4(remote) else { connection.cancel(); return }
        connection.start(queue: queue)
        queue.asyncAfter(deadline: .now() + 25) { connection.cancel() }
        receive(connection, remote: remote, buffer: Data())
    }
    private func receive(_ connection: NWConnection, remote: String, buffer: Data) {
        connection.receive(minimumIncompleteLength: 1, maximumLength: 65536) { [weak self] data, _, complete, error in
            guard let self = self else { return }; var accumulated = buffer
            if let bytes = data { accumulated.append(bytes) }
            guard accumulated.count <= 65536 else { connection.cancel(); return }
            if let newline = accumulated.firstIndex(of: 10) {
                do {
                    guard let request = try JSONSerialization.jsonObject(with: accumulated[..<newline]) as? [String: Any], let id = request["id"] as? String, id.count <= 64,
                          let action = request["action"] as? String,
                          request["windowsHost"] as? String == remote else { connection.cancel(); return }
                    let pairing = ["pair.begin", "pair.verify", "pair.finish"].contains(action)
                    if !pairing {
                        guard let supplied = request["token"] as? String, self.tokenMatches(supplied),
                              ["status", "stop", "extend", "deskflow"].contains(action) else { connection.cancel(); return }
                    }
                    self.handle(request, remote) { response in
                        var result = response; result["id"] = id
                        guard let body = try? JSONSerialization.data(withJSONObject: result) else { connection.cancel(); return }
                        connection.send(content: body + Data([10]), completion: .contentProcessed { _ in connection.cancel() })
                    }
                } catch { connection.cancel() }
            } else if complete || error != nil { connection.cancel() } else { self.receive(connection, remote: remote, buffer: accumulated) }
        }
    }
    private func tokenMatches(_ value: String) -> Bool {
        let expected = Array(identity.token.utf8), actual = Array(value.utf8)
        guard actual.count == expected.count else { return false }
        var difference: UInt8 = 0; for (a,b) in zip(expected,actual) { difference |= a ^ b }; return difference == 0
    }
}
