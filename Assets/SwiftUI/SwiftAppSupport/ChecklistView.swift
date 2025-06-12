import SwiftUI

struct ChecklistView: View {
    @ObservedObject var viewModel: ProtocolViewModel
    
    var body: some View {
        VStack(spacing: 8) {
            // Sign-off status header
            HStack {
                Image(systemName: viewModel.isStepSignedOff ? 
                    "checkmark.seal.fill" : "checkmark.seal")
                    .foregroundColor(viewModel.isStepSignedOff ? 
                        .green : .gray)
                    .animation(nil, value: viewModel.isStepSignedOff)
                
                Text(viewModel.isStepSignedOff ? 
                    "Step Signed Off" : "Step Not Signed Off")
                    .font(.subheadline)
                    .foregroundColor(viewModel.isStepSignedOff ? 
                        .green : .gray)
                    .animation(nil, value: viewModel.isStepSignedOff)
            }
            .padding(.horizontal)
            .padding(.top, 8)
            
            // checklist
            ScrollViewReader { proxy in
                List {
                    ForEach(viewModel.checklistItems) { item in
                        CheckItemView(
                            definition: item,
                            isChecked: viewModel.isStepSignedOff || viewModel.currentStates.first { 
                                $0.checkIndex == viewModel.getIndex(for: item) 
                            }?.isChecked ?? false
                        )
                        .id(viewModel.getIndex(for: item))
                        .disabled(viewModel.isStepSignedOff)
                        .animation(nil, value: viewModel.isStepSignedOff)
                    }
                }
                .animation(.default, value: viewModel.checklistItems)
                .onChange(of: viewModel.lastCheckedItemIndex) { _, newIndex in
                    if let nextUncheckedIndex = viewModel.checklistItems.indices.first(where: { index in
                        !(viewModel.currentStates.first { $0.checkIndex == index }?.isChecked ?? false)
                    }) {
                        withAnimation(.easeInOut(duration: 0.3)) {
                            proxy.scrollTo(nextUncheckedIndex, anchor: .center)
                        }
                    }
                }
            }
        }
        .transaction { transaction in
            transaction.animation = nil
        }
    }
}
