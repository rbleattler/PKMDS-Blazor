import Foundation

func usage(_ name: String) -> Never {
    FileHandle.standardError.write(Data("usage: \(name) <libPkmdsNative.dylib> <pkm|save> <file>\n".utf8))
    exit(64)
}

let argv = CommandLine.arguments
let progName = (argv.first as NSString?)?.lastPathComponent ?? "pkmds-poc"
guard argv.count >= 4 else { usage(progName) }

let dylibPath = argv[1]
let kind = argv[2]
let inputPath = argv[3]

let symbolName: String
switch kind {
case "pkm":  symbolName = "pkmds_describe_pkm"
case "save": symbolName = "pkmds_describe_save"
default:     usage(progName)
}

guard let handle = dlopen(dylibPath, RTLD_NOW) else {
    let err = dlerror().map { String(cString: $0) } ?? "unknown"
    FileHandle.standardError.write(Data("dlopen failed: \(err)\n".utf8))
    exit(1)
}
defer { dlclose(handle) }

guard let symbol = dlsym(handle, symbolName) else {
    FileHandle.standardError.write(Data("dlsym failed: \(symbolName) not exported\n".utf8))
    exit(1)
}

typealias DescribeFn = @convention(c) (
    UnsafePointer<UInt8>?, Int32, UnsafeMutablePointer<UInt8>?, Int32
) -> Int32
let describe = unsafeBitCast(symbol, to: DescribeFn.self)

let data: Data
do {
    data = try Data(contentsOf: URL(fileURLWithPath: inputPath))
} catch {
    FileHandle.standardError.write(Data("read failed: \(error)\n".utf8))
    exit(1)
}

let outCap = 64 * 1024
var outBuf = [UInt8](repeating: 0, count: outCap)

let written = data.withUnsafeBytes { (raw: UnsafeRawBufferPointer) -> Int32 in
    let ptr = raw.bindMemory(to: UInt8.self).baseAddress
    return outBuf.withUnsafeMutableBufferPointer { dst in
        describe(ptr, Int32(data.count), dst.baseAddress, Int32(outCap))
    }
}

if written < 0 {
    FileHandle.standardError.write(Data("\(symbolName) failed: code \(written)\n".utf8))
    exit(2)
}

let json = String(decoding: outBuf.prefix(Int(written)), as: UTF8.self)
print(json)
