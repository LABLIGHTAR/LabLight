import SwiftUI
import UnityFramework

struct ProtocolMenuContentView: View {
    @StateObject private var viewModel = ProtocolMenuViewModel()
    @Environment(\.dismiss) private var dismiss
    @State private var path = NavigationPath()
    @State private var showingCheckpointSheet = false
    @State private var unfinishedSessions: [CheckpointState] = []
    
    var body: some View {
        NavigationStack(path: $path) {
            VStack {
                Text("Select a Protocol")
                    .font(.largeTitle)
                    .padding()
                
                if viewModel.protocols.isEmpty {
                    Text("Loading protocols...")
                } else {
                    List {
                        ForEach(viewModel.protocols) { protocolDef in
                            Button(action: {
                                Task {
                                    do {
                                        let cpProvider = LocalFileCheckpointDataProvider()
                                        let userId = UserDefaults.standard.string(forKey:"currentUserID") ?? "anonymous"
                                        let sessions = try await cpProvider.loadStates(protocolName: protocolDef.title, userID: userId)
                                        if sessions.isEmpty {
                                            if let jsonData = try? JSONEncoder().encode(protocolDef),
                                               let jsonString = String(data: jsonData, encoding: .utf8) {
                                                CallCSharpCallback("selectProtocol|" + jsonString)
                                                path.append(protocolDef)
                                            }
                                        } else {
                                            unfinishedSessions = sessions
                                            showingCheckpointSheet = true
                                        }
                                    } catch {
                                        print("Failed to load checkpoint sessions: \\(error)")
                                    }
                                }
                            }) {
                                VStack(alignment: .leading, spacing: 4) {
                                    Text(protocolDef.title)
                                        .font(.headline)
                                    Text("Version: \(protocolDef.version)")
                                        .font(.subheadline)
                                        .foregroundColor(.gray)
                                    Text(protocolDef.description)
                                        .font(.caption)
                                        .foregroundColor(.secondary)
                                        .lineLimit(2)
                                }
                                .padding(.vertical, 4)
                            }
                        }
                    }
                }
            }
            .onAppear {
                viewModel.requestProtocolDefinitions()
            }
            .navigationDestination(for: ProtocolDefinition.self) { protocolDef in
                ProtocolView(selectedProtocol: protocolDef)
            }
            .ornament(visibility: .visible, attachmentAnchor: .scene(.leading)) {
                downloadButton
            }
            .sheet(isPresented: $showingCheckpointSheet) {
                if let protocolDef = path.last as? ProtocolDefinition ?? viewModel.protocols.first(where: { $0.title == unfinishedSessions.first?.protocolName }) {
                    CheckpointResumeSheet(
                        protocolName: protocolDef.title,
                        unfinished: unfinishedSessions,
                        onResume: { cp in
                            if let data = try? JSONEncoder().encode(cp),
                               let json = String(data: data, encoding: .utf8) {
                                CallCSharpCallback("resumeCheckpoint|" + json)
                            }
                        },
                        onNewRun: {
                            if let jsonData = try? JSONEncoder().encode(protocolDef),
                               let jsonString = String(data: jsonData, encoding: .utf8) {
                                CallCSharpCallback("selectProtocol|" + jsonString)
                                path.append(protocolDef)
                            }
                        },
                        onDelete: { cp in
                            Task {
                                let cpProvider = LocalFileCheckpointDataProvider()
                                try? await cpProvider.deleteState(sessionID: cp.sessionID)
                                unfinishedSessions.removeAll { $0.sessionID == cp.sessionID }
                            }
                        }
                    )
                }
            }
        }
    }
    
    private var downloadButton: some View {
        Button(action: {
            viewModel.downloadProtocol()
        }) {
            Image(systemName: "arrow.down.circle")
                .symbolEffect(.bounce, value: viewModel.isDownloadAvailable)
        }
        .disabled(!viewModel.isDownloadAvailable)
        .padding()
        .buttonStyle(.plain)
        .glassBackgroundEffect(in: RoundedRectangle(cornerRadius: 22))
        .tint(viewModel.isDownloadAvailable ? .blue : .gray)
    }
    
    func formatText(_ text: String) -> String {
        var formatted = text.components(separatedBy: CharacterSet.alphanumerics.inverted).joined()
        formatted = formatted.replacingOccurrences(of: "([a-z])([A-Z])", with: "$1 $2", options: .regularExpression, range: nil)
        return formatted.prefix(1).uppercased() + formatted.dropFirst()
    }
}

class ProtocolMenuViewModel: ObservableObject {
    @Published var protocols: [ProtocolDefinition] = []
    @Published var isDownloadAvailable: Bool = false
    
    init() {
        NotificationCenter.default.addObserver(self, selector: #selector(handleProtocolDefinitions(_:)), name: Notification.Name("ProtocolDefinitions"), object: nil)
        NotificationCenter.default.addObserver(self, selector: #selector(handleJsonFileDownloadable(_:)), name: Notification.Name("JsonFileDownloadable"), object: nil)
    }
    
    @objc func handleProtocolDefinitions(_ notification: Notification) {
        if let message = notification.userInfo?["message"] as? String,
           message.hasPrefix("protocolDefinitions|") {
            let protocolsJson = String(message.dropFirst("protocolDefinitions|".count))
            if let data: Data = protocolsJson.data(using: .utf8) {
                do {
                    let decoder = JSONDecoder()
                    // Try to decode array elements individually
                    let jsonArray = try JSONSerialization.jsonObject(with: data) as? [[String: Any]] ?? []
                    let decodedProtocols = jsonArray.compactMap { protocolDict -> ProtocolDefinition? in
                        guard let protocolData = try? JSONSerialization.data(withJSONObject: protocolDict) else { return nil }
                        return try? decoder.decode(ProtocolDefinition.self, from: protocolData)
                    }
                    
                    DispatchQueue.main.async {
                        self.protocols = decodedProtocols
                    }
                } catch {
                    print("######LABLIGHT Error decoding protocols: \(error)")
                }
            } else {
                print("######LABLIGHT Failed to create data from protocols JSON string")
            }
        }
    }
    
    @objc func handleJsonFileDownloadable(_ notification: Notification) {
        if let message = notification.userInfo?["message"] as? String,
           message.hasPrefix("jsonFileDownloadableChange|") {
            let jsonFileInfo = String(message.dropFirst("jsonFileDownloadableChange|".count))
            DispatchQueue.main.async {
                self.isDownloadAvailable = !jsonFileInfo.isEmpty
            }
        }
    }
    
    func requestProtocolDefinitions() {
        CallCSharpCallback("requestProtocolDefinitions|")
    }
    
    func downloadProtocol() {
        CallCSharpCallback("downloadJsonProtocol|")
        isDownloadAvailable = false
    }
}