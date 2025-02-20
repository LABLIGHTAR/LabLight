import SwiftUI
import UnityFramework

struct UserSelectionContentView: View {
    @StateObject private var viewModel = UserSelectionViewModel()
    @State private var showingCreateUser = false
    @State private var newUserName = ""
    
    var body: some View {
        VStack {
            Text("Select User")
                .font(.largeTitle)
                .padding()
            
            if viewModel.userProfiles.isEmpty {
                Text("Loading users...")
            } else {
                List {
                    ForEach(viewModel.userProfiles, id: \.userId) { profile in
                        Button(action: {
                            viewModel.selectUser(profile)
                        }) {
                            Text(profile.name)
                                .font(.headline)
                                .padding()
                        }
                    }
                }
            }
            
            Button(action: {
                showingCreateUser = true
            }) {
                Label("Add User", systemImage: "person.badge.plus")
            }
            .padding()
        }
        .sheet(isPresented: $showingCreateUser) {
            CreateUserView(isPresented: $showingCreateUser, viewModel: viewModel)
        }
        .onAppear {
            viewModel.requestUserProfiles()
        }
    }
}

struct CreateUserView: View {
    @Binding var isPresented: Bool
    @ObservedObject var viewModel: UserSelectionViewModel
    @State private var userName = ""
    
    var body: some View {
        NavigationView {
            Form {
                TextField("User Name", text: $userName)
            }
            .navigationTitle("Create User")
            .navigationBarItems(
                leading: Button("Cancel") {
                    isPresented = false
                },
                trailing: Button("Create") {
                    viewModel.createUser(name: userName)
                    isPresented = false
                }
                .disabled(userName.isEmpty)
            )
        }
    }
}

class UserSelectionViewModel: ObservableObject {
    @Published var userProfiles: [UserProfile] = []
    
    init() {
        NotificationCenter.default.addObserver(
            self,
            selector: #selector(handleUserProfiles(_:)),
            name: Notification.Name("UserProfiles"),
            object: nil
        )
    }
    
    @objc func handleUserProfiles(_ notification: Notification) {
        if let message = notification.userInfo?["message"] as? String,
           message.hasPrefix("userProfiles|") {
            let profilesJson = String(message.dropFirst("userProfiles|".count))
            if let data = profilesJson.data(using: .utf8) {
                do {
                    let profiles = try JSONDecoder().decode([UserProfile].self, from: data)
                    DispatchQueue.main.async {
                        self.userProfiles = profiles
                    }
                } catch {
                    print("######LABLIGHT Error decoding user profiles: \(error)")
                }
            }
        }
    }
    
    func requestUserProfiles() {
        CallCSharpCallback("requestUserProfiles|")
    }
    
    func selectUser(_ profile: UserProfile) {
        CallCSharpCallback("selectUser|\(profile.userId)")
    }
    
    func createUser(name: String) {
        CallCSharpCallback("createUser|\(name)")
        requestUserProfiles() // Refresh the list after creating
    }
}

struct UserProfile: Codable {
    let userId: String
    let name: String
}