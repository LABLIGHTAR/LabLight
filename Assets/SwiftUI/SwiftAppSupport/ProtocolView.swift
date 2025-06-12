import SwiftUI
import UnityFramework

struct ProtocolView: View {
    // MARK: - Properties
    @StateObject private var viewModel: ProtocolViewModel
    @Namespace private var animation
    @State private var showingPDFMenu = false
    @Environment(\.dismiss) private var dismiss
    @State private var showingCloseConfirmation = false
    
    // MARK: - Initialization
    init(selectedProtocol: ProtocolDefinition) {
        _viewModel = StateObject(wrappedValue: ProtocolViewModel(selectedProtocol: selectedProtocol))
    }
    
    // MARK: - Body
    var body: some View {
        ZStack {
            VStack(spacing: 16) {
                stepPicker
                
                contentArea
            }
            .animation(.spring(response: 0.3, dampingFraction: 0.8), value: viewModel.currentStep.contentItems.isEmpty)
            .navigationBarTitleDisplayMode(.inline)
            .navigationTitle(viewModel.selectedProtocol.title)
            .navigationBarBackButtonHidden(true)
            .ornament(visibility: .visible, attachmentAnchor: .scene(.leading)) {
                controlPanel
            }
            .interactiveDismissDisabled()
        }
        .toolbar {
            ToolbarItem(placement: .navigationBarLeading) {
                Button("Close") {
                    showingCloseConfirmation = true
                }
            }
        }
        .confirmationDialog(
            "Are you sure you want to close?",
            isPresented: $showingCloseConfirmation,
            titleVisibility: .visible
        ) {
            Button("Close Protocol", role: .destructive) {
                CallCSharpCallback("closeProtocol|true")
                dismiss()
            }
            Button("Cancel", role: .cancel) { }
        } message: {
            Text("Progress will be temporarily saved and metadata will be recorded on the server for review")
        }
    }
    
    // MARK: - View Components
    private var stepPicker: some View {
        ScrollViewReader { proxy in
            HStack {
                Spacer()
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: 8) {
                        ForEach(0..<viewModel.selectedProtocol.steps.count, id: \.self) { index in
                            Button(action: {
                                withAnimation(.easeInOut(duration: 0.3)) {
                                    viewModel.selectedStepIndex = index
                                }
                            }) {
                                Text("\(index + 1)")
                                    .padding(.horizontal, 12)
                                    .padding(.vertical, 8)
                                    .background {
                                        if index == viewModel.selectedStepIndex {
                                            RoundedRectangle(cornerRadius: 16)
                                                .fill(.white.opacity(0.15))
                                        } else {
                                            RoundedRectangle(cornerRadius: 16)
                                                .fill(.clear)
                                                .glassBackgroundEffect()
                                        }
                                    }
                                    .foregroundColor(index == viewModel.selectedStepIndex ? .primary : .white)
                            }
                            .buttonStyle(.plain)
                            .id(index)
                        }
                    }
                    .padding(.horizontal, 8)
                }
                .padding(.vertical, 4)
                .background {
                    Capsule()
                        .fill(.black.opacity(0.3))
                }
                Spacer()
            }
            .onChange(of: viewModel.selectedStepIndex) { _, newValue in
                withAnimation(.easeInOut(duration: 0.3)) {
                    proxy.scrollTo(newValue, anchor: .center)
                }
            }
        }
        .padding(.horizontal)
    }
    
    private var contentArea: some View {
        HStack(spacing: 0) {
            ChecklistView(viewModel: viewModel)
                .frame(maxWidth: shouldShowContent ? 400 : .infinity)
            
            if shouldShowContent {
                ProtocolContentView(
                    contentItems: viewModel.currentStep.contentItems ?? [],
                    nextUncheckedItem: viewModel.nextUncheckedItem()
                )
                .frame(maxWidth: .infinity)
                .padding(.leading, 10)
                .transition(.asymmetric(
                    insertion: .opacity.combined(with: .move(edge: .trailing)).animation(.easeInOut(duration: 0.3)),
                    removal: .opacity.combined(with: .move(edge: .trailing)).animation(.easeInOut(duration: 0.3))
                ))
            }
        }
        .padding(.horizontal)
        .animation(.easeInOut(duration: 0.3), value: shouldShowContent)
    }
    
    private var shouldShowContent: Bool {
        // Filter step content items
        let hasStepContent = (viewModel.currentStep.contentItems ?? [])
            .contains { $0.arObjectID == nil || $0.arObjectID.isEmpty }
        
        // Check next item's content items (if any exist and aren't AR-related)
        let hasNextItemContent = viewModel.nextUncheckedItem()?.contentItems
            .contains { $0.arObjectID == nil || $0.arObjectID.isEmpty } ?? false
        
        return hasStepContent || hasNextItemContent
    }
    
    private var controlPanel: some View {
        VStack(spacing: 20) {
            checkButton
            uncheckButton
            navigationButtons
            signOffButton
            pdfButton
        }
        .padding()
        .buttonStyle(.plain)
        .glassBackgroundEffect(in: RoundedRectangle(cornerRadius: 22))
    }
    
    private var checkButton: some View {
        Button(action: viewModel.checkNextItem) {
            Image(systemName: "checkmark")
        }
        .disabled(viewModel.nextUncheckedItem() == nil || viewModel.isStepSignedOff)
        .padding(EdgeInsets(top: 4, leading: 8, bottom: 4, trailing: 8))
    }
    
    private var uncheckButton: some View {
        Button(action: viewModel.uncheckLastItem) {
            Image(systemName: "xmark")
        }
        .disabled(viewModel.lastCheckedItem() == nil || viewModel.isStepSignedOff)
        .padding(EdgeInsets(top: 4, leading: 8, bottom: 4, trailing: 8))
    }
    
    private var navigationButtons: some View {
        Group {
            Button(action: viewModel.goToNextStep) {
                Image(systemName: "chevron.right")
            }
            .disabled(viewModel.selectedStepIndex >= viewModel.selectedProtocol.steps.count - 1)
            .padding(EdgeInsets(top: 4, leading: 8, bottom: 4, trailing: 8))
            
            Button(action: viewModel.goToPreviousStep) {
                Image(systemName: "chevron.left")
            }
            .disabled(viewModel.selectedStepIndex == 0)
            .padding(EdgeInsets(top: 4, leading: 8, bottom: 4, trailing: 8))
        }
    }
    
    private var signOffButton: some View {
        Button(action: viewModel.signOffStep) {
            Image(systemName: "checkmark.seal")
        }
        .disabled(!viewModel.canSignOff)
        .padding(EdgeInsets(top: 4, leading: 8, bottom: 4, trailing: 8))
    }
    
    private var pdfButton: some View {
        Group {
            if !viewModel.selectedProtocol.protocolPDFNames.isEmpty {
                Menu {
                    ForEach(viewModel.selectedProtocol.protocolPDFNames, id: \.self) { pdfName in
                        Button(action: { viewModel.openPDF(pdfName) }) {
                            Label(pdfName, systemImage: "doc.richtext")
                        }
                    }
                } label: {
                    Image(systemName: "doc.richtext")
                        .padding(EdgeInsets(top: 4, leading: 8, bottom: 4, trailing: 8))
                }
            }
        }
    }
}

// MARK: - ViewModel
class ProtocolViewModel: ObservableObject {
    // MARK: - Properties
    let selectedProtocol: ProtocolDefinition
    @Published var selectedStepIndex: Int = 0 {
        didSet {
            if selectedStepIndex != oldValue {
                CallCSharpCallback("stepNavigation|" + String(selectedStepIndex))
            }
        }
    }
    @Published var checklistItems: [CheckItemDefinition] = []
    @Published var currentStates: [CheckItemStateData] = []
    @Published var lastCheckedItemIndex: Int?
    @Published var isStepSignedOff: Bool = false
    @Published var canSignOff: Bool = false
    
    // MARK: - Initialization
    init(selectedProtocol: ProtocolDefinition) {
        self.selectedProtocol = selectedProtocol
        self.checklistItems = selectedProtocol.steps.first?.checklist ?? []
        setupNotifications()
    }
    
    // MARK: - Setup
    private func setupNotifications() {
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(handleStepChange(_:)),
            name: Notification.Name("StepChange"),
            object: nil
        )
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(handleCheckItemChange(_:)),
            name: Notification.Name("CheckItemChange"),
            object: nil
        )
    }
    
    // MARK: - Computed Properties
    var currentStep: StepDefinition {
        selectedProtocol.steps[selectedStepIndex]
    }
    
    // MARK: - Public Methods
    func getItemState(for item: CheckItemDefinition) -> CheckItemStateData {
        let index = getIndex(for: item)
        let isChecked = currentStates.first { $0.checkIndex == index }?.isChecked ?? false
        return CheckItemStateData(isChecked: isChecked, checkIndex: index)
    }
    
    func getIndex(for item: CheckItemDefinition) -> Int {
        checklistItems.firstIndex(of: item) ?? 0
    }
    
    func nextUncheckedItem() -> CheckItemDefinition? {
        return checklistItems.first { item in
            let index = getIndex(for: item)
            return !(currentStates.first { $0.checkIndex == index }?.isChecked ?? false)
        }
    }

    func lastCheckedItem() -> CheckItemDefinition? {
        return checklistItems.last { item in
            let index = getIndex(for: item)
            return currentStates.first { $0.checkIndex == index }?.isChecked ?? false
        }
    }
    
    // MARK: - Actions
    func checkNextItem() {
        if let nextItem = nextUncheckedItem() {
            CallCSharpCallback("checkItem|" + String(getIndex(for: nextItem)))
        }
    }

    func uncheckLastItem() {
        if let lastItem = lastCheckedItem() {
            CallCSharpCallback("uncheckItem|" + String(getIndex(for: lastItem)))
        }
    }

    func goToNextStep() {
        if selectedStepIndex < selectedProtocol.steps.count - 1 {
            selectedStepIndex += 1
        }
    }

    func goToPreviousStep() {
        if selectedStepIndex > 0 {
            selectedStepIndex -= 1
        }
    }

    func openPDF(_ pdfName: String) {
        CallCSharpCallback("requestPDF|" + pdfName)
    }
    
    func signOffStep() {
        CallCSharpCallback("checklistSignOff|true")
        isStepSignedOff = true
        currentStates = checklistItems.enumerated().map { index, _ in
            CheckItemStateData(isChecked: true, checkIndex: index)
        }
        updateCanSignOff()
    }
    
    // MARK: - Notification Handlers
    @objc func handleStepChange(_ notification: Notification) {
        guard let message = notification.userInfo?["message"] as? String,
              message.hasPrefix("stepChange|"),
              let data = message.dropFirst("stepChange|".count).data(using: .utf8),
              let stepStateData = try? JSONDecoder().decode(StepStateData.self, from: data)
        else {
            print("Invalid step change message format")
            return
        }
        
        DispatchQueue.main.async {
            if self.selectedStepIndex != stepStateData.currentStepIndex {
                self.selectedStepIndex = stepStateData.currentStepIndex
            }
            
            self.checklistItems = self.currentStep.checklist
            if let checklistState = stepStateData.checklistState {
                self.currentStates = checklistState
            } else if stepStateData.isSignedOff {
                self.currentStates = self.checklistItems.enumerated().map { index, _ in
                    CheckItemStateData(isChecked: true, checkIndex: index)
                }
            }
            self.lastCheckedItemIndex = self.currentStates.last { $0.isChecked }?.checkIndex
            self.isStepSignedOff = stepStateData.isSignedOff
            self.updateCanSignOff()
        }
    }

    @objc func handleCheckItemChange(_ notification: Notification) {
        guard let message = notification.userInfo?["message"] as? String,
              message.hasPrefix("checkItemChange|"),
              let data = message.dropFirst("checkItemChange|".count).data(using: .utf8),
              let checkItemStateDataList = try? JSONDecoder().decode([CheckItemStateData].self, from: data)
        else {
            print("Invalid check item change message format")
            return
        }
        
        DispatchQueue.main.async {
            self.currentStates = checkItemStateDataList
            self.lastCheckedItemIndex = checkItemStateDataList.last { $0.isChecked }?.checkIndex
            self.updateCanSignOff()
        }
    }
    
    private func updateCanSignOff() {
        canSignOff = !isStepSignedOff &&
                    currentStates.count == checklistItems.count &&
                    currentStates.allSatisfy { $0.isChecked }
    }
}

// MARK: - Models
struct StepStateData: Codable {
    let currentStepIndex: Int
    let isSignedOff: Bool
    let checklistState: [CheckItemStateData]?
    
    enum CodingKeys: String, CodingKey {
        case currentStepIndex = "CurrentStepIndex"
        case isSignedOff = "IsSignedOff"
        case checklistState = "Checklist"
    }
}

struct CheckItemStateData: Codable {
    let isChecked: Bool
    let checkIndex: Int
    
    enum CodingKeys: String, CodingKey {
        case isChecked = "IsChecked"
        case checkIndex = "CheckIndex"
    }
}
