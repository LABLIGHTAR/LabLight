import SwiftUI
import UnityFramework

class LLMChatViewModel: ObservableObject {
    @Published var messages: [ChatMessage] = []
    @Published var isAuthenticated: Bool = false
    @Published var loginError: String?
    
    init() {
        NotificationCenter.default.addObserver(self, selector: #selector(handleMessage(_:)), name: Notification.Name("LLMChatMessage"), object: nil)
    }
    
    @objc func handleMessage(_ notification: Notification) {
        if let message = notification.userInfo?["message"] as? String {
            DispatchQueue.main.async {
                if message.hasPrefix("authStatus|") {
                    let status = message.dropFirst("authStatus|".count)
                    self.isAuthenticated = (status == "True")
                    if !self.isAuthenticated {
                        self.loginError = "Authentication failed. Please try again."
                    }
                } else {
                    let chatMessage = message.replacingOccurrences(of: "LLMChatMessage|", with: "")
                    self.addMessage(chatMessage, isUser: false)
                }
            }
        }
    }
    
    func addMessage(_ content: String, isUser: Bool) {
        DispatchQueue.main.async {
            self.messages.append(ChatMessage(content: content, isUser: isUser))
        }
    }
}

struct LLMChatContentView: View {
    @StateObject private var viewModel = LLMChatViewModel()
    @State private var email: String = ""
    @State private var password: String = ""
    @State private var inputText: String = ""
    
    var body: some View {
        Group {
            if viewModel.isAuthenticated {
                chatView
            } else {
                loginView
            }
        }
    }
    
    var loginView: some View {
        VStack(spacing: 20) {
            Text("Please login to access LLM chat features")
                .font(.headline)
                .multilineTextAlignment(.center)
                .padding(.bottom)
            
            TextField("Email", text: $email)
                .textFieldStyle(RoundedBorderTextFieldStyle())
                .autocapitalization(.none)
                .keyboardType(.emailAddress)
            SecureField("Password", text: $password)
                .textFieldStyle(RoundedBorderTextFieldStyle())
            Button("Login") {
                if isValidEmail(email) && !password.isEmpty {
                    CallCSharpCallback("login|" + email + "," + password)
                } else {
                    viewModel.loginError = "Please enter a valid email and password."
                }
            }
            .foregroundColor(.white)
            .cornerRadius(8)
            
            if let error = viewModel.loginError {
                Text(error)
                    .foregroundColor(.red)
            }
        }
        .padding(30)
    }
    
    var chatView: some View {
        VStack {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 10) {
                    ForEach(viewModel.messages) { message in
                        ChatBubble(message: message)
                    }
                }
                .padding(.horizontal, 20)
                .padding(.vertical, 10)
            }
            .padding(.top, 10)
            
            HStack {
                TextField("Type a message", text: $inputText)
                    .textFieldStyle(RoundedBorderTextFieldStyle())
                
                Button("Send") {
                    if !inputText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                        CallCSharpCallback("sendMessage|" + inputText)
                        viewModel.addMessage(inputText, isUser: true)
                        inputText = ""
                    }
                }
                .background(Color.blue)
                .foregroundColor(.white)
                .cornerRadius(8)
            }
            .padding(.horizontal, 20)
            .padding(.vertical, 10)
        }
        .padding(.vertical, 20)
    }
    
    func isValidEmail(_ email: String) -> Bool {
        let emailRegEx = "[A-Z0-9a-z._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,64}"
        let emailPred = NSPredicate(format:"SELF MATCHES %@", emailRegEx)
        return emailPred.evaluate(with: email)
    }
}

struct ChatMessage: Identifiable {
    let id = UUID()
    let content: String
    let isUser: Bool
}

struct ChatBubble: View {
    let message: ChatMessage
    
    var body: some View {
        HStack {
            if message.isUser { Spacer() }
            Text(message.content)
                .padding()
                .background(message.isUser ? Color.blue : Color.gray)
                .foregroundColor(.white)
                .cornerRadius(10)
            if !message.isUser { Spacer() }
        }
    }
}