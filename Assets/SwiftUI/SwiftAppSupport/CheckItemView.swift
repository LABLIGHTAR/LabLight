import SwiftUI

struct CheckItemView: View {
    let definition: CheckItemDefinition
    let isChecked: Bool
    
    var body: some View {
        HStack {
            Toggle(isOn: .constant(isChecked)) {
                Text(definition.text)
                    .strikethrough(isChecked, color: .white)
                    .foregroundColor(isChecked ? .gray : .white)
            }
            .toggleStyle(CheckBoxToggleStyle())
            .disabled(true)
        }
    }
}

// Custom Toggle Style to resemble a Checkbox
struct CheckBoxToggleStyle: ToggleStyle {
    func makeBody(configuration: Configuration) -> some View {
        Button(action: { configuration.isOn.toggle() }) {
            HStack {
                Image(systemName: configuration.isOn ? "checkmark.square" : "square")
                    .foregroundColor(configuration.isOn ? .blue : .gray)
                configuration.label
            }
        }
        .buttonStyle(PlainButtonStyle())
        .disabled(true)
    }
}
