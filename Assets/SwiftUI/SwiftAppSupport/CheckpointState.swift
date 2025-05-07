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