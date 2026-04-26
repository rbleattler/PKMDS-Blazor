import Foundation
import WebKit

enum BridgeError: Error, LocalizedError {
    case notAttached
    case invalidExport
    case timeout
    case loadSaveFailed(String)
    case requestExportFailed(String)

    var errorDescription: String? {
        switch self {
        case .notAttached: return "WKWebView not attached"
        case .invalidExport: return "Couldn't decode exported save bytes"
        case .timeout: return "Export timed out (no saveExport message received)"
        case .loadSaveFailed(let msg): return "loadSave failed: \(msg)"
        case .requestExportFailed(let msg): return "requestExport failed: \(msg)"
        }
    }
}

@MainActor
final class PkmdsBridge: NSObject, ObservableObject {
    @Published private(set) var isReady = false
    @Published private(set) var lastError: String?

    private let save: PickedSave
    private weak var webView: WKWebView?

    private var exportCallback: ((Result<ExportedSave, Error>) -> Void)?
    private var exportTimeoutTask: Task<Void, Never>?

    init(save: PickedSave) {
        self.save = save
    }

    func attach(webView: WKWebView) {
        self.webView = webView
    }

    func handle(kind: String, payload: [String: Any]) {
        switch kind {
        case "ready":
            isReady = true
            loadSave()
        case "saveExport":
            guard let dataB64 = payload["data"] as? String,
                  let data = Data(base64Encoded: dataB64) else {
                completeExport(.failure(BridgeError.invalidExport))
                return
            }
            let fileName = (payload["fileName"] as? String) ?? save.fileName
            completeExport(.success(ExportedSave(bytes: data, fileName: fileName)))
        default:
            print("[PKMDS bridge] ignoring unknown kind: \(kind)")
        }
    }

    private func loadSave() {
        guard let webView else { return }
        let b64 = save.bytes.base64EncodedString()
        let escapedName = save.fileName
            .replacingOccurrences(of: "\\", with: "\\\\")
            .replacingOccurrences(of: "'", with: "\\'")
        let js = "window.PKMDS.host.loadSave('\(b64)', '\(escapedName)')"
        webView.evaluateJavaScript(js) { [weak self] _, error in
            if let error {
                Task { @MainActor in
                    self?.lastError = "loadSave failed: \(error.localizedDescription)"
                }
            }
        }
    }

    func requestExport(timeout: TimeInterval = 5,
                       completion: @escaping (Result<ExportedSave, Error>) -> Void) {
        guard let webView else {
            completion(.failure(BridgeError.notAttached))
            return
        }
        exportCallback = completion
        exportTimeoutTask = Task { [weak self] in
            try? await Task.sleep(nanoseconds: UInt64(timeout * 1_000_000_000))
            guard !Task.isCancelled else { return }
            await MainActor.run {
                self?.completeExport(.failure(BridgeError.timeout))
            }
        }
        webView.evaluateJavaScript("window.PKMDS.host.requestExport()") { [weak self] _, error in
            if let error {
                Task { @MainActor in
                    self?.completeExport(
                        .failure(BridgeError.requestExportFailed(error.localizedDescription))
                    )
                }
            }
        }
    }

    private func completeExport(_ result: Result<ExportedSave, Error>) {
        exportTimeoutTask?.cancel()
        exportTimeoutTask = nil
        let cb = exportCallback
        exportCallback = nil
        cb?(result)
    }
}

final class PkmdsMessageHandler: NSObject, WKScriptMessageHandler {
    weak var bridge: PkmdsBridge?

    init(bridge: PkmdsBridge) {
        self.bridge = bridge
    }

    func userContentController(_ controller: WKUserContentController,
                               didReceive message: WKScriptMessage) {
        guard let body = message.body as? [String: Any],
              let kind = body["kind"] as? String else { return }
        Task { @MainActor in
            self.bridge?.handle(kind: kind, payload: body)
        }
    }
}
