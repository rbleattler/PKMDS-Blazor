import Foundation
import WebKit

final class PkmdsSchemeHandler: NSObject, WKURLSchemeHandler {
    static let scheme = "app"
    static let host = "pkmds"

    private let bundleRoot: URL

    init(bundleRoot: URL) {
        self.bundleRoot = bundleRoot
    }

    func webView(_ webView: WKWebView, start urlSchemeTask: WKURLSchemeTask) {
        guard let url = urlSchemeTask.request.url else {
            urlSchemeTask.didFailWithError(URLError(.badURL))
            return
        }

        var path = url.path
        if path.hasPrefix("/") { path.removeFirst() }
        if path.isEmpty { path = "index.html" }

        let resolved = bundleRoot.appendingPathComponent(path)

        guard FileManager.default.fileExists(atPath: resolved.path),
              let response = HTTPURLResponse(
                url: url,
                statusCode: 200,
                httpVersion: "HTTP/1.1",
                headerFields: nil),
              let data = try? Data(contentsOf: resolved)
        else {
            let notFound = HTTPURLResponse(
                url: url,
                statusCode: 404,
                httpVersion: "HTTP/1.1",
                headerFields: ["Content-Type": "text/plain"])!
            urlSchemeTask.didReceive(notFound)
            urlSchemeTask.didReceive(Data("Not found: \(url.path)".utf8))
            urlSchemeTask.didFinish()
            return
        }

        let (mimeType, encoding) = mimeAndEncoding(for: resolved)
        var headers: [String: String] = [
            "Content-Type": mimeType,
            "Content-Length": "\(data.count)",
            "Cache-Control": "no-store"
        ]
        if let encoding {
            headers["Content-Encoding"] = encoding
        }
        let final = HTTPURLResponse(
            url: response.url ?? url,
            statusCode: 200,
            httpVersion: "HTTP/1.1",
            headerFields: headers)!
        urlSchemeTask.didReceive(final)
        urlSchemeTask.didReceive(data)
        urlSchemeTask.didFinish()
    }

    func webView(_ webView: WKWebView, stop urlSchemeTask: WKURLSchemeTask) {}

    private func mimeAndEncoding(for url: URL) -> (String, String?) {
        var path = url.path
        var encoding: String?
        if path.hasSuffix(".br") {
            path = String(path.dropLast(3))
            encoding = "br"
        } else if path.hasSuffix(".gz") {
            path = String(path.dropLast(3))
            encoding = "gzip"
        }
        let ext = (path as NSString).pathExtension.lowercased()
        let mime: String
        switch ext {
        case "html", "htm": mime = "text/html; charset=utf-8"
        case "js", "mjs": mime = "application/javascript; charset=utf-8"
        case "css": mime = "text/css; charset=utf-8"
        case "json": mime = "application/json; charset=utf-8"
        case "wasm": mime = "application/wasm"
        case "dll", "pdb", "blat", "dat", "bin": mime = "application/octet-stream"
        case "png": mime = "image/png"
        case "jpg", "jpeg": mime = "image/jpeg"
        case "gif": mime = "image/gif"
        case "svg": mime = "image/svg+xml"
        case "ico": mime = "image/x-icon"
        case "webp": mime = "image/webp"
        case "woff": mime = "font/woff"
        case "woff2": mime = "font/woff2"
        case "ttf": mime = "font/ttf"
        case "otf": mime = "font/otf"
        case "txt", "map": mime = "text/plain; charset=utf-8"
        case "xml": mime = "application/xml; charset=utf-8"
        default: mime = "application/octet-stream"
        }
        return (mime, encoding)
    }
}
