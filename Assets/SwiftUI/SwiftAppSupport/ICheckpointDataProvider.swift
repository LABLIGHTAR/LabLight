import Foundation

@MainActor
protocol ICheckpointDataProvider {
    func saveState(_ state: CheckpointState) async throws
    func updateState(_ state: CheckpointState) async throws
    func loadStates(protocolName: String, userID: String) async throws -> [CheckpointState]
    func deleteState(sessionID: UUID) async throws
}