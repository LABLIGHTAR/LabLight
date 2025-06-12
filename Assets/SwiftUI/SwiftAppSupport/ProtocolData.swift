import Foundation
import Combine

// MARK: - Protocol Definition

struct ProtocolDefinition: Codable, Identifiable, Equatable, Hashable {
    let version: String
    let title: String
    let description: String
    let protocolPDFNames: [String]
    let globalArObjects: [ArObject]
    let steps: [StepDefinition]
    let mediaBasePath: String?
    
    // Computed property for Identifiable
    var id: String { title }
    
    // MARK: Coding Keys
    enum CodingKeys: String, CodingKey {
        case version, title, description, protocolPDFNames, globalArObjects, steps, mediaBasePath
    }
    
    // MARK: Hashable
    func hash(into hasher: inout Hasher) {
        hasher.combine(id)
        hasher.combine(version)
    }
    
    // MARK: Equatable
    static func == (lhs: ProtocolDefinition, rhs: ProtocolDefinition) -> Bool {
        return lhs.id == rhs.id &&
               lhs.version == rhs.version &&
               lhs.title == rhs.title &&
               lhs.description == rhs.description &&
               lhs.protocolPDFNames == rhs.protocolPDFNames &&
               lhs.globalArObjects == rhs.globalArObjects &&
               lhs.steps == rhs.steps &&
               lhs.mediaBasePath == rhs.mediaBasePath
    }
}

// MARK: - Step Definition

struct StepDefinition: Codable, Identifiable, Equatable, Hashable {
    let isCritical: Bool
    let estimatedDurationInSeconds: Int
    let contentItems: [ContentItem]
    let checklist: [CheckItemDefinition]
    let title: String?
    
    // Computed property for Identifiable
    let id: UUID = UUID()
    
    private enum CodingKeys: String, CodingKey {
        case isCritical
        case estimatedDurationInSeconds
        case contentItems
        case checklist
        case title
    }
    
    // MARK: Hashable
    func hash(into hasher: inout Hasher) {
        hasher.combine(id)
    }
    
    // MARK: Equatable
    static func == (lhs: StepDefinition, rhs: StepDefinition) -> Bool {
        return lhs.id == rhs.id &&
               lhs.isCritical == rhs.isCritical &&
               lhs.estimatedDurationInSeconds == rhs.estimatedDurationInSeconds &&
               lhs.contentItems == rhs.contentItems &&
               lhs.checklist == rhs.checklist &&
               lhs.title == rhs.title
    }
}

// MARK: - Check Item Definition

struct CheckItemDefinition: Codable, Identifiable, Equatable, Hashable {
    let text: String
    let contentItems: [ContentItem]
    let arActions: [ArAction]
    
    // Computed property for Identifiable
    let id: UUID = UUID()
    
    enum CodingKeys: String, CodingKey {
        case text = "Text"
        case contentItems
        case arActions
    }
    
    // MARK: Hashable
    func hash(into hasher: inout Hasher) {
        hasher.combine(id)
    }
    
    // MARK: Equatable
    static func == (lhs: CheckItemDefinition, rhs: CheckItemDefinition) -> Bool {
        return lhs.id == rhs.id &&
               lhs.text == rhs.text &&
               lhs.contentItems == rhs.contentItems &&
               lhs.arActions == rhs.arActions
    }
}

// MARK: - Content Item

struct ContentItem: Codable, Identifiable, Equatable, Hashable {
    let contentType: String
    let properties: [String: String]
    let arObjectID: String
    
    let id: UUID = UUID()
    
    private enum CodingKeys: String, CodingKey {
        case contentType
        case properties
        case arObjectID
    }
    
    // Add init to handle missing arObjectID
    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        contentType = try container.decode(String.self, forKey: .contentType)
        properties = try container.decode([String: String].self, forKey: .properties)
        arObjectID = try container.decodeIfPresent(String.self, forKey: .arObjectID) ?? ""
    }
    
    // MARK: Hashable
    func hash(into hasher: inout Hasher) {
        hasher.combine(id)
    }
    
    // MARK: Equatable
    static func == (lhs: ContentItem, rhs: ContentItem) -> Bool {
        return lhs.id == rhs.id &&
               lhs.contentType == rhs.contentType &&
               lhs.properties == rhs.properties &&
               lhs.arObjectID == rhs.arObjectID
    }
}

// MARK: - AR Object

struct ArObject: Codable, Identifiable, Equatable, Hashable {
    let specificObjectName: String
    let arObjectID: String
    let rootPrefabName: String
    
    var id: String { arObjectID }
    
    private enum CodingKeys: String, CodingKey {
        case specificObjectName
        case arObjectID
        case rootPrefabName
    }
}

struct ArAction: Codable, Identifiable, Equatable, Hashable {
    let actionType: String
    let properties: [String: Any]
    let arObjectID: String
    
    let id: UUID = UUID()
    
    private enum CodingKeys: String, CodingKey {
        case actionType
        case properties
        case arObjectID
    }
    
    // Add init to handle missing arObjectID and decode properties
    init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        actionType = try container.decode(String.self, forKey: .actionType)
        
        // Decode properties as a dictionary that can contain either String or [String]
        let propertiesContainer = try container.decode([String: AnyCodable].self, forKey: .properties)
        properties = propertiesContainer.mapValues { $0.value }
        
        arObjectID = try container.decodeIfPresent(String.self, forKey: .arObjectID) ?? ""
    }
    
    // Add encoding support for properties
    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(actionType, forKey: .actionType)
        try container.encode(properties.mapValues { AnyCodable($0) }, forKey: .properties)
        try container.encode(arObjectID, forKey: .arObjectID)
    }
    
    // MARK: Hashable
    func hash(into hasher: inout Hasher) {
        hasher.combine(id)
    }
    
    // MARK: Equatable
    static func == (lhs: ArAction, rhs: ArAction) -> Bool {
        return lhs.id == rhs.id &&
               lhs.actionType == rhs.actionType &&
               NSDictionary(dictionary: lhs.properties).isEqual(to: rhs.properties) &&
               lhs.arObjectID == rhs.arObjectID
    }
}

// Helper type to handle encoding/decoding of Any values
private struct AnyCodable: Codable {
    let value: Any
    
    init(_ value: Any) {
        self.value = value
    }
    
    init(from decoder: Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let stringValue = try? container.decode(String.self) {
            value = stringValue
        } else if let arrayValue = try? container.decode([String].self) {
            value = arrayValue
        } else {
            throw DecodingError.dataCorruptedError(in: container, debugDescription: "Unsupported type")
        }
    }
    
    func encode(to encoder: Encoder) throws {
        var container = encoder.singleValueContainer()
        if let stringValue = value as? String {
            try container.encode(stringValue)
        } else if let arrayValue = value as? [String] {
            try container.encode(arrayValue)
        } else {
            throw EncodingError.invalidValue(value, EncodingError.Context(codingPath: encoder.codingPath, debugDescription: "Unsupported type"))
        }
    }
}
