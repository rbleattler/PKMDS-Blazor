import SwiftUI
import UIKit

struct EditorSheet: View {
    let save: PickedSave
    let onDismiss: () -> Void

    @StateObject private var bridge: PkmdsBridge
    @State private var exportItem: ExportItem?
    @State private var isExporting = false
    @State private var errorMessage: String?

    init(save: PickedSave, onDismiss: @escaping () -> Void) {
        self.save = save
        self.onDismiss = onDismiss
        _bridge = StateObject(wrappedValue: PkmdsBridge(save: save))
    }

    var body: some View {
        NavigationView {
            ZStack(alignment: .bottom) {
                PkmdsWebView(bridge: bridge)
                    .ignoresSafeArea(.container, edges: .bottom)

                if let errorMessage {
                    Text(errorMessage)
                        .font(.footnote)
                        .padding()
                        .background(Color.red.opacity(0.85))
                        .foregroundColor(.white)
                        .cornerRadius(8)
                        .padding()
                }
            }
            .navigationTitle(save.fileName)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { onDismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button(action: done) {
                        if isExporting {
                            ProgressView()
                        } else {
                            Text("Done").bold()
                        }
                    }
                    .disabled(!bridge.isReady || isExporting)
                }
            }
            .sheet(item: $exportItem) { item in
                ActivityView(activityItems: [item.url]) {
                    onDismiss()
                }
            }
        }
        .navigationViewStyle(.stack)
    }

    private func done() {
        errorMessage = nil
        isExporting = true
        bridge.requestExport { result in
            isExporting = false
            switch result {
            case .success(let exported):
                do {
                    let tempURL = FileManager.default.temporaryDirectory
                        .appendingPathComponent(exported.fileName)
                    try? FileManager.default.removeItem(at: tempURL)
                    try exported.bytes.write(to: tempURL, options: .atomic)
                    exportItem = ExportItem(url: tempURL)
                } catch {
                    errorMessage = "Couldn't stage exported file: \(error.localizedDescription)"
                }
            case .failure(let error):
                errorMessage = error.localizedDescription
            }
        }
    }
}

private struct ExportItem: Identifiable {
    let id = UUID()
    let url: URL
}

private struct ActivityView: UIViewControllerRepresentable {
    let activityItems: [Any]
    let onComplete: () -> Void

    func makeUIViewController(context: Context) -> UIActivityViewController {
        let vc = UIActivityViewController(activityItems: activityItems, applicationActivities: nil)
        vc.completionWithItemsHandler = { _, _, _, _ in
            onComplete()
        }
        return vc
    }

    func updateUIViewController(_ uiViewController: UIActivityViewController, context: Context) {}
}
