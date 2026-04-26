import SwiftUI

@main
struct PkmdsHostApp: App {
    var body: some Scene {
        WindowGroup("PKMDS Quick Look (POC)") {
            ContentView()
        }
    }
}

struct ContentView: View {
    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                Text("PKMDS Quick Look")
                    .font(.title.bold())
                Text("Proof of Concept")
                    .font(.title3)
                    .foregroundStyle(.secondary)

                Divider()

                Text("This host app exists so iOS will register the bundled Quick Look extension. Once installed, long-press a `.pk*` or `.sav` file in **Files** (or any other Quick Look surface — Mail attachments, AirDrop received files) to preview it.")
                    .fixedSize(horizontal: false, vertical: true)

                Text("Supported types")
                    .font(.headline)
                    .padding(.top, 8)
                Text("• `.pk1`–`.pk9`, `.pa8`, `.pb7`, `.pb8` — Pokémon entity files")
                Text("• `.sav` — Pokémon save files")
            }
            .padding(24)
            .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}
