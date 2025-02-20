// Any swift file whose name ends in "InjectedScene" is expected to contain
// a computed static "scene" property like the one below. It will be injected to the top
// level App's Scene. The name of the class/struct must match the name of the file.

import Foundation
import SwiftUI
import SafariServices

struct SwiftUIInjectedScene {
    @SceneBuilder
    static var scene: some Scene {
        // You can create multiple WindowGroups here for different wnidows;
        // they need a distinct id. If you include multiple items,
        // the scene property must be decorated with "@SceneBuilder" as above.

        WindowGroup(id: "UserProfiles") {
            UserSelectionContentView()
        }
        .defaultSize(width: 400, height: 600)

        WindowGroup(id: "ProtocolMenu") {
            ProtocolMenuContentView()
        }
        .defaultSize(width: 800, height: 600)

        WindowGroup(id: "PDF", for: String.self) { $pdfUrl in
            PDFContentView(pdfUrl!)
        }
        .defaultSize(width: 500.0, height: 800.0)
        
        WindowGroup(id: "Safari", for: String.self) { $urlString in
            SafariContentView(defaultUrlString: urlString!)
        }
        .defaultSize(width: 800.0, height: 800.0)
        
        WindowGroup(id: "Video", for: String.self) { $videoUrl in
            VideoContentView(videoUrl!)
        }
        .defaultSize(width: 900.0, height: 700.0)

        WindowGroup(id: "LLMChat") {
            LLMChatContentView()
        }
        .defaultSize(width: 400, height: 600)

        WindowGroup(id: "Timer", for: Int.self) { $duration in
            TimerContentView(duration)
        }
        .defaultSize(width: 300, height: 400)

        WindowGroup(id: "Calculator") {
            CalculatorContentView()
        }
        .defaultSize(width: 400, height: 750)

        WindowGroup(id: "SimpleText") {
            Text("PDF Window")
        }
    }
}
