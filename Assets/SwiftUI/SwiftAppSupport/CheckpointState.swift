import Foundation

/// Codable model that matches the cross-platform checkpoint JSON schema.
struct CheckpointState: Codable, Identifiable {
    static let currentSchemaVersion = 1

    // MARK: – Top-level
    var schemaVersion: Int = currentSchemaVersion
    var sessionID: UUID               = UUID()
    var userID: String
    var protocolName: String
    var protocolVersion: String
    var startTimestamp: Date          = Date()
    var completionTimestamp: Date?

    // MARK: – Steps
    var steps: [StepProgress]         = []

    // MARK: – Review meta
    var reviewMeta: [String: CodableValue] = [:]

    // Identifiable
    var id: UUID { sessionID }

    // MARK: Nested types
    struct StepProgress: Codable {
        var stepIndex: Int
        var title: String?
        var startTime: Date?
        var signoffTime: Date?
        var signoffUserID: String?
        var checkItems: [CheckItemProgress] = []
    }

    struct CheckItemProgress: Codable {
        var index: Int
        var text: String?
        var completedTime: Date?
        var completedBy: String?
    }
}

/// Wrapper that allows heterogenous dictionary values to be safely encoded.
enum CodableValue: Codable {
    case string(String)
    case int(Int)
    case double(Double)
    case bool(Bool)

    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let v = try? container.decode(String.self)  { self = .string(v) }
        else if let v = try? container.decode(Int.self){ self = .int(v) }
        else if let v = try? container.decode(Double.self){ self = .double(v) }
        else if let v = try? container.decode(Bool.self){ self = .bool(v) }
        else { throw DecodingError.typeMismatch(CodableValue.self,
                                                .init(codingPath: decoder.codingPath,
                                                      debugDescription: "Unsupported type")) }
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        switch self {
        case .string(let v):  try container.encode(v)
        case .int(let v):     try container.encode(v)
        case .double(let v):  try container.encode(v)
        case .bool(let v):    try container.encode(v)
        }
    }
}

// MARK: - Progress helpers ---------------------------------------------------

extension CheckpointState.StepProgress {
    /// Number of checklist items already completed.
    var completedItemsCount: Int {
        checkItems.filter { $0.completedTime != nil }.count
    }
}

extension CheckpointState {

    /// Index of the first unsigned-off step, or the last step if all signed off.
    var currentStepIndex: Int {
        steps.firstIndex(where: { $0.signoffTime == nil }) ?? max(steps.count - 1, 0)
    }

    /// Convenience accessor for the currently active StepProgress.
    var currentStep: StepProgress? {
        guard !steps.isEmpty else { return nil }
        return steps[currentStepIndex]
    }

    /// Returns `(current, total, awaitingSignOffFlag)` for the *current* step’s checklist.
    func itemProgress() -> (current: Int, total: Int, awaitingSignOff: Bool) {
        guard let step = currentStep else { return (0, 0, false) }

        let total = step.checkItems.count
        let completed = step.completedItemsCount
        let awaiting = completed == total && step.signoffTime == nil
        return (completed, total, awaiting)
    }

    /// Latest timestamp found in the checkpoint (completion, sign-off or session start).
    var lastActivity: Date {
        var latest = startTimestamp
        for s in steps {
            if let t = s.signoffTime, t > latest { latest = t }
            for ci in s.checkItems {
                if let t = ci.completedTime, t > latest { latest = t }
            }
        }
        return latest
    }
}