import Foundation

@main struct MacPairingTests {
    @MainActor static func main() throws {
        var count = 0
        func check(_ value: Bool, _ name: String) throws {
            guard value else { throw FlowError.message(name) }
            count += 1; print("PASS \(name)")
        }
        func reject(_ name: String, _ operation: () throws -> Void) throws {
            var rejected = false
            do { try operation() } catch { rejected = true }
            try check(rejected, name)
        }
        let nonce = Data(repeating: 7, count: 32).base64EncodedString()
        let serverNonce = Data(repeating: 9, count: 32).base64EncodedString()
        let fingerprint = String(repeating: "A", count: 64)
        try check(PairingGate.commitment(nonce) == "EBBAD97F478755D90C8F34BEC04444DC8B0025D5FC09ED3523AA5D17E6059D86", "commitment matches Windows vector")
        try check(PairingGate.verificationCode(fingerprint, nonce, serverNonce) == "398057", "verification matches Windows vector")
        let gate = PairingGate(), host = "192.168.1.20", token = "test-secret"
        let begin: [String: Any] = ["action": "pair.begin", "clientCommit": PairingGate.commitment(nonce)]
        try reject("closed pairing window refuses discovery") { _ = try gate.handle(begin, host: host, fingerprint: fingerprint, token: token) }
        gate.enable()
        let challenge = try gate.handle(begin, host: host, fingerprint: fingerprint, token: token)
        try check(challenge["token"] == nil && gate.code.isEmpty, "challenge withholds secret and awaits nonce reveal")
        try reject("one local approval window accepts one request") { _ = try gate.handle(begin, host: host, fingerprint: fingerprint, token: token) }
        var finish: [String: Any] = ["action": "pair.finish", "session": challenge["session"]!, "clientNonce": nonce]
        try reject("finish before nonce verification rejected") { _ = try gate.handle(finish, host: host, fingerprint: fingerprint, token: token) }
        var verify = finish; verify["action"] = "pair.verify"
        var altered = verify; altered["clientNonce"] = Data(repeating: 8, count: 32).base64EncodedString()
        try reject("changed committed nonce rejected") { _ = try gate.handle(altered, host: host, fingerprint: fingerprint, token: token) }
        _ = try gate.handle(verify, host: host, fingerprint: fingerprint, token: token)
        let code = PairingGate.verificationCode(fingerprint, nonce, challenge["serverNonce"] as! String)
        try check(gate.code == code, "Mac displays certificate-bound comparison code")
        let pending = try gate.handle(finish, host: host, fingerprint: fingerprint, token: token)
        try check(pending["state"] as? String == "pending" && pending["token"] == nil, "unapproved request cannot obtain secret")
        gate.approve()
        try reject("approval cannot be claimed from another IP") { _ = try gate.handle(finish, host: "192.168.1.21", fingerprint: fingerprint, token: token) }
        let approved = try gate.handle(finish, host: host, fingerprint: fingerprint, token: token)
        try check(approved["state"] as? String == "approved" && approved["token"] as? String == token, "local approval releases secret to matching session")
        gate.finish(host: host)
        try check(!gate.requestPending && gate.code.isEmpty, "authenticated connection completes pairing")
        try reject("completed session cannot be reused") { _ = try gate.handle(finish, host: host, fingerprint: fingerprint, token: token) }
        finish["session"] = "missing"
        try reject("unknown pairing session rejected") { _ = try gate.handle(finish, host: host, fingerprint: fingerprint, token: token) }
        print("TOTAL \(count) passed")
    }
}
