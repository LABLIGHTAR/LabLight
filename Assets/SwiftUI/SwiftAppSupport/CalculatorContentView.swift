import SwiftUI
import Foundation

class CalculatorViewModel: ObservableObject {
    @Published var displayValue: String = "0"
    @Published var operationDisplay: String = ""
    
    private var currentValue: Double = 0
    private var storedValue: Double = 0
    private var currentOperation: CalculatorButton?
    private var shouldResetDisplay = true
    
    func performAction(for button: CalculatorButton) {
        switch button {
        case .clear:
            clear()
        case .equals:
            calculateResult()
        case .add, .subtract, .multiply, .divide, .power:
            setOperation(button)
            updateOperationDisplay()
        case .decimal:
            addDecimal()
        case .percent:
            calculatePercent()
        case .negative:
            toggleSign()
        case .sin, .cos, .tan, .log, .sqrt:
            performUnaryOperation(button)
        default:
            appendDigit(button)
        }
    }
    
    private func clear() {
        displayValue = "0"
        currentValue = 0
        storedValue = 0
        currentOperation = nil
        shouldResetDisplay = true
        operationDisplay = ""
    }
    
    private func calculateResult() {
        guard let operation = currentOperation else { return }
        
        let secondValue = Double(displayValue) ?? 0
        
        switch operation {
        case .add:
            currentValue = storedValue + secondValue
        case .subtract:
            currentValue = storedValue - secondValue
        case .multiply:
            currentValue = storedValue * secondValue
        case .divide:
            currentValue = storedValue / secondValue
        case .power:
            currentValue = pow(storedValue, secondValue)
        default:
            break
        }
        
        displayValue = formatResult(currentValue)
        shouldResetDisplay = true
        currentOperation = nil
        operationDisplay = ""
    }
    
    private func setOperation(_ operation: CalculatorButton) {
        if currentOperation != nil {
            calculateResult()
        }
        storedValue = Double(displayValue) ?? 0
        currentOperation = operation
        shouldResetDisplay = true
    }
    
    private func updateOperationDisplay() {
        operationDisplay = "\(formatResult(storedValue)) \(currentOperation?.title ?? "")"
    }
    
    private func addDecimal() {
        if !displayValue.contains(".") {
            displayValue += "."
            shouldResetDisplay = false
        }
    }
    
    private func calculatePercent() {
        if let value = Double(displayValue) {
            displayValue = formatResult(value / 100)
        }
    }
    
    private func toggleSign() {
        if var value = Double(displayValue) {
            value.negate()
            displayValue = formatResult(value)
        }
    }
    
    private func performUnaryOperation(_ operation: CalculatorButton) {
        guard let value = Double(displayValue) else { return }
        
        switch operation {
        case .sin:
            currentValue = sin(value)
        case .cos:
            currentValue = cos(value)
        case .tan:
            currentValue = tan(value)
        case .log:
            currentValue = log10(value)
        case .sqrt:
            currentValue = sqrt(value)
        default:
            break
        }
        
        displayValue = formatResult(currentValue)
        shouldResetDisplay = true
    }
    
    private func appendDigit(_ button: CalculatorButton) {
        let digit = button.title
        
        if shouldResetDisplay {
            displayValue = digit
            shouldResetDisplay = false
        } else {
            displayValue += digit
        }
    }
    
    private func setNumber(_ number: Double) {
        displayValue = formatResult(number)
        shouldResetDisplay = true
    }
    
    private func formatResult(_ result: Double) -> String {
        return String(format: "%g", result)
    }
}

enum CalculatorButton: Hashable {
    case digit(Int), zero, decimal, equals, add, subtract, multiply, divide
    case clear, negative, percent
    case sin, cos, tan, log, sqrt, power, pi, e
    
    var title: String {
        switch self {
        case .digit(let num): return String(num)
        case .zero: return "0"
        case .decimal: return "."
        case .equals: return "="
        case .add: return "+"
        case .subtract: return "-"
        case .multiply: return "×"
        case .divide: return "÷"
        case .clear: return "AC"
        case .negative: return "±"
        case .percent: return "%"
        case .sin: return "sin"
        case .cos: return "cos"
        case .tan: return "tan"
        case .log: return "log"
        case .sqrt: return "√"
        case .power: return "xʸ"
        case .pi: return "π"
        case .e: return "e"
        }
    }
    
    var backgroundColor: Color {
        switch self {
        case .clear, .negative, .percent:
            return .gray.opacity(0.3)
        case .divide, .multiply, .subtract, .add, .equals:
            return .orange
        case .sin, .cos, .tan, .log, .sqrt, .power, .pi, .e:
            return .blue.opacity(0.7)
        default:
            return Color(.darkGray).opacity(0.3)
        }
    }
}

extension CalculatorButton {
    static let one = CalculatorButton.digit(1)
    static let two = CalculatorButton.digit(2)
    static let three = CalculatorButton.digit(3)
    static let four = CalculatorButton.digit(4)
    static let five = CalculatorButton.digit(5)
    static let six = CalculatorButton.digit(6)
    static let seven = CalculatorButton.digit(7)
    static let eight = CalculatorButton.digit(8)
    static let nine = CalculatorButton.digit(9)
}

struct CalculatorButtonView: View {
    let button: CalculatorButton
    let action: () -> Void
    
    var body: some View {
        Button(action: action) {
            Text(button.title)
                .font(.system(size: 32))
                .foregroundColor(.white)
                .frame(width: buttonWidth(for: button), height: 72)
                .background(button.backgroundColor)
                .clipShape(RoundedRectangle(cornerRadius: 36))
        }
        .frame(width: buttonWidth(for: button), height: 72)
    }
    
    private func buttonWidth(for button: CalculatorButton) -> CGFloat {
        switch button {
        case .zero:
            return 156
        default:
            return 72
        }
    }
}

struct CalculatorContentView: View {
    @StateObject private var viewModel = CalculatorViewModel()
    
    let buttons: [[CalculatorButton]] = [
        [.clear, .negative, .percent, .divide],
        [.seven, .eight, .nine, .multiply],
        [.four, .five, .six, .subtract],
        [.one, .two, .three, .add],
        [.zero, .decimal, .equals],
        [.sin, .cos, .tan, .log],
        [.sqrt, .power, .pi, .e]
    ]
    
    var body: some View {
        VStack(spacing: 12) {
            Spacer()
            
            VStack(alignment: .trailing) {
                Text(viewModel.operationDisplay)
                    .font(.system(size: 24))
                    .foregroundColor(.gray)
                Text(viewModel.displayValue)
                    .font(.system(size: 64))
                    .foregroundColor(.white)
            }
            .frame(maxWidth: .infinity, alignment: .trailing)
            .padding()
            
            ForEach(buttons, id: \.self) { row in
                HStack(spacing: 12) {
                    ForEach(row, id: \.self) { button in
                        CalculatorButtonView(button: button, action: {
                            viewModel.performAction(for: button)
                        })
                    }
                }
            }
        }
        .padding(20)
        .frame(minWidth: 400, minHeight: 700)
    }
}
