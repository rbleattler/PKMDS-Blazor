import Foundation

struct PickedSave: Identifiable {
    let id = UUID()
    let bytes: Data
    let fileName: String
}

struct ExportedSave {
    let bytes: Data
    let fileName: String
}
