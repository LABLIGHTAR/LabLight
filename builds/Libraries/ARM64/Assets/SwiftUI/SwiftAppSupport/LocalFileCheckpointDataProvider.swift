import Foundation
import os.log

actor LocalFileCheckpointDataProvider: ICheckpointDataProvider {

    private let rootURL: URL
    private let logger = Logger(subsystem: "com.lablight.save", category: "checkpoint")

    init() {
        let docs = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0]
        self.rootURL = docs.appendingPathComponent("Checkpoints", isDirectory: true)
        try? FileManager.default.createDirectory(at: rootURL, withIntermediateDirectories: true)
    }

    // MARK: - Public API
    func saveState(_ state: CheckpointState) async throws {
        try await writeState(state)
    }

    func updateState(_ state: CheckpointState) async throws {
        try await writeState(state)
    }

    func loadStates(protocolName: String, userID: String) async throws -> [CheckpointState] {
        let start = Date()
        var results: [CheckpointState] = []

        let pattern = "\(safe(userID))_\(safe(protocolName))_"
        for file in try FileManager.default.contentsOfDirectory(at: rootURL,
                                                                includingPropertiesForKeys: nil) {
            guard file.lastPathComponent.hasPrefix(pattern),
                  file.pathExtension == "json" else { continue }

            do {
                let data = try Data(contentsOf: file)
                let state = try JSONDecoder().decode(CheckpointState.self, from: data)
                if state.completionTimestamp == nil { results.append(state) }
            } catch {
                logger.error("ts=\(self.ts()) action=LoadStates parseErr file=\(file.path) err=\(error.localizedDescription)")
            }
        }

        logger.debug("ts=\(self.ts()) action=LoadStates count=\(results.count) elapsedMs=\(Date().timeIntervalSince(start)*1000.0, format: .fixed(0))")
        return results.sorted { $0.startTimestamp > $1.startTimestamp }
    }

    func deleteState(sessionID: UUID) async throws {
        let pattern = "_\(sessionID).json"
        for file in try FileManager.default.contentsOfDirectory(at: rootURL,
                                                                includingPropertiesForKeys: nil)
            where file.lastPathComponent.hasSuffix(pattern) {
                do {
                    try FileManager.default.removeItem(at: file)
                    logger.debug("ts=\(self.ts()) action=DeleteState sessionID=\(sessionID) status=success")
                } catch {
                    logger.error("ts=\(self.ts()) action=DeleteState sessionID=\(sessionID) status=failed err=\(error.localizedDescription)")
                    throw error
                }
        }
    }

    // MARK: - Internal helpers
    private func writeState(_ state: CheckpointState) async throws {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        encoder.dateEncodingStrategy = .iso8601

        let data = try encoder.encode(state)
        let finalURL = path(for: state)
        let tmpURL   = finalURL.appendingPathExtension("tmp")

        var attempt = 0
        let delays: [UInt64] = [100, 500, 2000] // ms

        while attempt < delays.count {
            do {
                try data.write(to: tmpURL, options: .atomic)
                // Replace existing if any
                if FileManager.default.fileExists(atPath: finalURL.path) {
                    try FileManager.default.removeItem(at: finalURL)
                }
                try FileManager.default.moveItem(at: tmpURL, to: finalURL)
                logger.debug("ts=\(self.ts()) action=SaveState file=\(finalURL.lastPathComponent) size=\(data.count) status=success")
                return
            } catch {
                logger.error("ts=\(self.ts()) action=SaveState attempt=\(attempt) status=error err=\(error.localizedDescription)")
                try await Task.sleep(nanoseconds: delays[attempt]*1_000_000)
                attempt += 1
            }
        }

        logger.error("ts=\(self.ts()) action=SaveState status=fatal")
        throw CocoaError(.fileWriteUnknown)
    }

    private func path(for state: CheckpointState) -> URL {
        let file = "\(safe(state.userID))_\(safe(state.protocolName))_\(state.sessionID).json"
        return rootURL.appendingPathComponent(file)
    }

    private func safe(_ str: String) -> String {
        let invalid = CharacterSet(charactersIn: "/\\: ")
        var result = ""
        for scalar in str.unicodeScalars {
            result.append(invalid.contains(scalar) ? "_" : Character(scalar))
        }
        return result
    }

    private func ts() -> String {
        ISO8601DateFormatter().string(from: Date())
    }
}
