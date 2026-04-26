import SwiftUI
import WebKit

struct PkmdsWebView: UIViewRepresentable {
    let bridge: PkmdsBridge

    func makeUIView(context: Context) -> WKWebView {
        let bundleRoot = Bundle.main.url(forResource: "PKMDS", withExtension: nil)!
        let schemeHandler = PkmdsSchemeHandler(bundleRoot: bundleRoot)
        let messageHandler = PkmdsMessageHandler(bridge: bridge)

        let config = WKWebViewConfiguration()
        config.setURLSchemeHandler(schemeHandler, forURLScheme: PkmdsSchemeHandler.scheme)
        config.userContentController.add(messageHandler, name: "pkmds")

        let webView = WKWebView(frame: .zero, configuration: config)
        webView.allowsBackForwardNavigationGestures = false
        // PKMDS scrolls the document itself; let it use the full WebView bounds rather
        // than reserving home-indicator inset that doesn't match the page's content size.
        // Without this the inner scroll view rubberbands back from the bottom.
        webView.scrollView.contentInsetAdjustmentBehavior = .never
        if #available(iOS 16.4, *) {
            webView.isInspectable = true
        }
        bridge.attach(webView: webView)

        // WKWebViewConfiguration retains the scheme handler weakly in some iOS versions —
        // pin both helpers to the SwiftUI coordinator so they live as long as the view.
        context.coordinator.schemeHandler = schemeHandler
        context.coordinator.messageHandler = messageHandler

        // Load the root path, not /index.html — Blazor's router matches `@page "/"`,
        // and `/index.html` falls through to PKMDS's 404 page. The scheme handler
        // serves index.html for empty paths transparently.
        let url = URL(string: "\(PkmdsSchemeHandler.scheme)://\(PkmdsSchemeHandler.host)/?host=poc-ios")!
        webView.load(URLRequest(url: url))
        return webView
    }

    func updateUIView(_ uiView: WKWebView, context: Context) {}

    func makeCoordinator() -> Coordinator { Coordinator() }

    final class Coordinator {
        var schemeHandler: PkmdsSchemeHandler?
        var messageHandler: PkmdsMessageHandler?
    }
}
