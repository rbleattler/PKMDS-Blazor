import SwiftUI
import UniformTypeIdentifiers

struct ContentView: View {
    @State private var pickerPresented = false
    @State private var pickedSave: PickedSave?
    @State private var errorMessage: String?

    var body: some View {
        NavigationView {
            VStack(spacing: 24) {
                Spacer()

                Image(systemName: "square.and.pencil")
                    .font(.system(size: 64))
                    .foregroundColor(.accentColor)

                VStack(spacing: 8) {
                    Text("PKMDS Host (PoC)")
                        .font(.title2.bold())
                    Text("Pick a Pokémon save file. Bytes round-trip through the embedded WKWebView bridge.")
                        .font(.subheadline)
                        .foregroundColor(.secondary)
                        .multilineTextAlignment(.center)
                        .padding(.horizontal)
                }

                Button {
                    pickerPresented = true
                } label: {
                    Label("Pick a save…", systemImage: "doc.badge.plus")
                        .font(.headline)
                        .frame(maxWidth: .infinity)
                        .padding()
                        .background(Color.accentColor)
                        .foregroundColor(.white)
                        .cornerRadius(12)
                }
                .padding(.horizontal)

                if let errorMessage {
                    Text(errorMessage)
                        .font(.caption)
                        .foregroundColor(.red)
                        .multilineTextAlignment(.center)
                        .padding(.horizontal)
                }

                Spacer()
            }
            .navigationBarHidden(true)
        }
        .navigationViewStyle(.stack)
        .fileImporter(
            isPresented: $pickerPresented,
            allowedContentTypes: [.data],
            allowsMultipleSelection: false
        ) { result in
            switch result {
            case .success(let urls):
                if let url = urls.first {
                    load(url: url)
                }
            case .failure(let error):
                errorMessage = "File picker failed: \(error.localizedDescription)"
            }
        }
        .sheet(item: $pickedSave) { picked in
            EditorSheet(save: picked) {
                pickedSave = nil
            }
        }
    }

    private func load(url: URL) {
        let granted = url.startAccessingSecurityScopedResource()
        defer { if granted { url.stopAccessingSecurityScopedResource() } }
        do {
            let bytes = try Data(contentsOf: url)
            pickedSave = PickedSave(bytes: bytes, fileName: url.lastPathComponent)
            errorMessage = nil
        } catch {
            errorMessage = "Couldn't read file: \(error.localizedDescription)"
        }
    }
}
