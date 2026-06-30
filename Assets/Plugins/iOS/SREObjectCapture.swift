// Native on-device Object Capture bridge for iOS / visionOS — RealityKit
// PhotogrammetrySession reconstructs a USDZ from a folder of photos. Paired with
// Assets/Scripts/ARCore/AppleObjectCaptureService.cs. We do our own guided
// capture (FurnitureCaptureController), so this only does the reconstruction step.
//
// Requirements: iOS 17+ on a device where PhotogrammetrySession.isSupported is
// true (Object Capture is hardware-gated). No extra frameworks beyond RealityKit.
// Verify the PhotogrammetrySession output handling on device — the API is
// async/Swift-only, so this is bridged via @_cdecl + a C completion callback.

import Foundation
import RealityKit

private var sreOcCallback: (@convention(c) (Bool, UnsafePointer<CChar>?) -> Void)? = nil

private func sreReport(_ ok: Bool, _ message: String) {
    message.withCString { ptr in sreOcCallback?(ok, ptr) }
}

@_cdecl("_sreObjectCaptureSetCallback")
public func _sreObjectCaptureSetCallback(_ cb: @escaping @convention(c) (Bool, UnsafePointer<CChar>?) -> Void) {
    sreOcCallback = cb
}

@_cdecl("_sreObjectCaptureSupported")
public func _sreObjectCaptureSupported() -> Bool {
    if #available(iOS 17.0, *) { return PhotogrammetrySession.isSupported }
    return false
}

/// Reconstruct a USDZ at `outputPath` from the JPEGs in `inputDir`. Async; the
/// result (success + output path / error message) is delivered to the callback
/// registered above.
@_cdecl("_sreObjectCaptureReconstruct")
public func _sreObjectCaptureReconstruct(_ inputDir: UnsafePointer<CChar>, _ outputPath: UnsafePointer<CChar>) {
    let input = String(cString: inputDir)
    let output = String(cString: outputPath)

    guard #available(iOS 17.0, *) else {
        sreReport(false, "Object Capture requires iOS 17 or later")
        return
    }

    Task.detached {
        do {
            let session = try PhotogrammetrySession(input: URL(fileURLWithPath: input))
            try session.process(requests: [ .modelFile(url: URL(fileURLWithPath: output)) ])

            for try await update in session.outputs {
                switch update {
                case .processingComplete:
                    sreReport(true, output)
                    return
                case .requestError(_, let error):
                    sreReport(false, error.localizedDescription)
                    return
                case .processingCancelled:
                    sreReport(false, "cancelled")
                    return
                default:
                    continue // progress / sample updates
                }
            }
            sreReport(false, "no model produced")
        } catch {
            sreReport(false, error.localizedDescription)
        }
    }
}
