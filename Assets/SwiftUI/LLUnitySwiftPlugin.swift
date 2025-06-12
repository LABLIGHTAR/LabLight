import Foundation
import SwiftUI
import PDFKit

// MARK: - Callback Delegate

typealias CallbackDelegateType = @convention(c) (UnsafePointer<CChar>) -> Void

private var sCallbackDelegate: CallbackDelegateType? = nil

// Make this variable public and accessible globally
public var CallCSharpCallback: (String) -> Void = { str in
    str.withCString { sCallbackDelegate?($0) }
}

@_cdecl("SetNativeCallback")
func setNativeCallback(_ delegate: CallbackDelegateType) {
    sCallbackDelegate = delegate
    CallCSharpCallback = { str in
        str.withCString { sCallbackDelegate?($0) }
    }
}

public func CallCSharpCallback(_ str: String) {
    if let callback = sCallbackDelegate {
        str.withCString { callback($0) }
    }
}

// MARK: - Unity to SwiftUI Message Passing
@_cdecl("SendMessageToSwiftUI")
func sendMessageToSwiftUI(_ cmessage: UnsafePointer<CChar>) {
    let message = String(cString: cmessage)
    //print("######LABLIGHT Message Received from Unity \(message)")
    if message.hasPrefix("protocolDefinitions|") {
        print("######LABLIGHT Posting ProtocolDefinitions notification")
        NotificationCenter.default.post(name: Notification.Name("ProtocolDefinitions"), object: nil, userInfo: ["message": message])
    }
    else if message.hasPrefix("protocolChange|") {
        print("######LABLIGHT Posting ProtocolChange notification")
        NotificationCenter.default.post(name: Notification.Name("ProtocolChange"), object: nil, userInfo: ["message": message])
    }
    else if message.hasPrefix("stepChange|") {
        print("######LABLIGHT Posting StepChange notification")
        NotificationCenter.default.post(name: Notification.Name("StepChange"), object: nil, userInfo: ["message": message])
    }
    else if message.hasPrefix("checkItemChange|") {
        print("######LABLIGHT Posting CheckItemChange notification")
        NotificationCenter.default.post(name: Notification.Name("CheckItemChange"), object: nil, userInfo: ["message": message])
    }
    else if message.hasPrefix("jsonFileDownloadableChange|") {
        print("######LABLIGHT Posting JsonFileDownloadableChange notification")
        NotificationCenter.default.post(name: Notification.Name("JsonFileDownloadableChange"), object: nil, userInfo: ["message": message])
    }
    else if message.hasPrefix("userProfiles|") {
        print("######LABLIGHT Posting UserProfiles notification")
        NotificationCenter.default.post(name: Notification.Name("UserProfiles"), object: nil, userInfo: ["message": message])
    }
    else {
        NotificationCenter.default.post(name: Notification.Name("LLMChatMessage"), object: nil, userInfo: ["message": message])
    }
}

// MARK: - Window Management

@_cdecl("OpenSwiftUIWindow")
func openSwiftUIWindow(_ cname: UnsafePointer<CChar>) {
    let name = String(cString: cname)
    print("######LABLIGHT Attempting to open window: \(name)")
    DispatchQueue.main.async {
        do {
            let openWindow = EnvironmentValues().openWindow
            openWindow(id: name)
            print("######LABLIGHT Successfully opened window: \(name)")
        } catch {
            print("######LABLIGHT Error opening window: \(name) - \(error)")
        }
    }
}

@_cdecl("CloseSwiftUIWindow")
func closeSwiftUIWindow(_ cname: UnsafePointer<CChar>) {
    let name = String(cString: cname)
    print("######LABLIGHT Attempting to close window: \(name)")
    DispatchQueue.main.async {
        do {
            let dismissWindow = EnvironmentValues().dismissWindow
            dismissWindow(id: name)
            print("######LABLIGHT Successfully closed window: \(name)")
        } catch {
            print("######LABLIGHT Error closing window: \(name) - \(error)")
        }
    }
}

// MARK: - PDF Functionality

@_cdecl("OpenSwiftPdfWindow")
func openSwiftPdfWindow(_ cname: UnsafePointer<CChar>) {
    let pdfString = String(cString: cname)
    let openWindow = EnvironmentValues().openWindow
    openWindow(id: "PDF", value: pdfString)
}

// MARK: - Video Functionality

@_cdecl("OpenSwiftVideoWindow")
func openSwiftVideoWindow(_ cname: UnsafePointer<CChar>) {
    let videoTitle = String(cString: cname)
    let openWindow = EnvironmentValues().openWindow
    openWindow(id: "Video", value: videoTitle)
}

// MARK: - Safari Functionality

@_cdecl("OpenSwiftSafariWindow")
func openSwiftSafariWindow(_ cname: UnsafePointer<CChar>) {
    let urlString = String(cString: cname)
    let openWindow = EnvironmentValues().openWindow
    openWindow(id: "Safari", value: urlString)
}

// MARK: - Timer Functionality

@_cdecl("OpenSwiftTimerWindow")
func openSwiftTimerWindow(_ duration: Int32) {
    let openWindow = EnvironmentValues().openWindow
    openWindow(id: "Timer", value: Int(duration))
}

// MARK: - Scientific Calculator Functionality

@_cdecl("OpenSwiftCalculatorWindow")
func openSwiftScientificCalculatorWindow() {
    let openWindow = EnvironmentValues().openWindow
    openWindow(id: "Calculator")
}

// MARK: - Any additional functionality...
