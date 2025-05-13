import SwiftUI
import os.log

struct CheckpointResumeSheet: View {
    @Environment(\.dismiss) private var dismiss
    @State private var isBusy = false

    let protocolName: String
    @State var unfinished: [CheckpointState]

    let onResume : (CheckpointState) -> Void
    let onNewRun : () -> Void
    let onDelete : (CheckpointState) -> Void

    private let log = Logger(subsystem: "com.lablight.ui", category: "checkpointUI")

    var body: some View {
        NavigationStack {
            VStack {
                if unfinished.isEmpty {
                    Text("No unfinished runs").padding()
                } else {
                    List {
                        ForEach(unfinished) { cp in
                            HStack {
                                VStack(alignment: .leading) {
                                    // Top line – original start date stays
                                    Text(DateFormatter.localizedString(from: cp.startTimestamp,
                                                                       dateStyle: .medium,
                                                                       timeStyle: .short))
                                        .font(.headline)

                                    // New: step / item progress + last update
                                    let stepStr = "Step \(cp.currentStepIndex + 1)/\(cp.steps.count)"
                                    let itemProg = cp.itemProgress()
                                    let itemStr = itemProg.awaitingSignOff
                                        ? "Checklist awaiting sign-off"
                                        : "Item \(itemProg.current + 1)/\(itemProg.total)"
                                    let lastStr = DateFormatter.localizedString(from: cp.lastActivity,
                                                                                dateStyle: .none,
                                                                                timeStyle: .short)
                                    Text("\(stepStr)  |  \(itemStr)  |  Last \(lastStr)")
                                        .font(.caption)
                                        .foregroundColor(.secondary)
                                }
                                Spacer()
                                Menu {
                                    Button("Resume") { resume(cp) }
                                    Button("Delete", role: .destructive) { delete(cp) }
                                } label: {
                                    Image(systemName: "ellipsis.circle")
                                        .font(.title2)
                                }
                            }
                            .padding(.vertical, 4)
                        }
                    }
                }

                Button("Start New Run") {
                    log.debug("ui=sheet action=newRun")
                    dismiss()
                    onNewRun()
                }
                .buttonStyle(.borderedProminent)
                .padding()
            }
            .navigationTitle("Resume \(protocolName)")
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Close") { dismiss() }
                }
            }
        }
    }

    private func resume(_ cp: CheckpointState) {
        log.debug("ui=sheet action=resume id=\(cp.sessionID)")
        dismiss()
        onResume(cp)
    }

    private func delete(_ cp: CheckpointState) {
        log.debug("ui=sheet action=delete id=\(cp.sessionID)")
        onDelete(cp)
    }
}