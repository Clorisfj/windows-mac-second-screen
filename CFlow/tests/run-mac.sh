#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p tests/private
xcrun swiftc -swift-version 5 -parse-as-library -target "$(uname -m)-apple-macos14.0" \
  Mac/Sources/Receiver.swift Mac/Sources/PairingGate.swift tests/MacPairingTests.swift \
  -framework Network -framework Security -framework CryptoKit -framework Combine \
  -o tests/private/MacPairingTests
tests/private/MacPairingTests
