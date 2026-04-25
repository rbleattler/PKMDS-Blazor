import SwiftUI

@main
struct PkmdsHostApp: App {
    var body: some Scene {
        WindowGroup("PKMDS Quick Look (POC)") {
            ContentView()
                .frame(minWidth: 420, minHeight: 220)
        }
    }
}

struct ContentView: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("PKMDS Quick Look — Proof of Concept")
                .font(.title2.bold())
            Text("This host app exists so macOS will register the bundled Quick Look extension. Press Space on a `.pk*` or `.sav` file in Finder to preview.")
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
        .padding(24)
    }
}
