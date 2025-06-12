//
// This custom View is referenced by SwiftUIInjectedScene
// to provide the body of a WindowGroup. It's part of the Unity-VisionOS
// target because it lives inside a "SwiftAppSupport" directory (and Unity
// will move it to that target).
//

import Foundation
import SwiftUI
import UnityFramework
import PDFKit

struct PDFContentView: View {
    let pdfUrlString: String
    @State private var errorMessage: String?
    @State private var pdfURL: URL?
    
    init(_ pdfUrlString: String) {
        self.pdfUrlString = pdfUrlString
    }
    
    var body: some View {
        if let url = pdfURL {
            PDFKitView(url: url)
        } else if let error = errorMessage {
            VStack {
                Image(systemName: "doc.text.fill")
                    .font(.largeTitle)
                    .foregroundColor(.red)
                Text(error)
                    .foregroundColor(.red)
                    .padding()
            }
            .frame(height: 200)
            .frame(maxWidth: .infinity)
            .background(Color(.systemGray6))
            .cornerRadius(8)
        } else {
            ProgressView()
                .onAppear {
                    loadPDF()
                }
        }
    }
    
    private func loadPDF() {
        // Try remote URL first
        if pdfUrlString.hasPrefix("http"), let url = URL(string: pdfUrlString) {
            pdfURL = url
            return
        }
        
        // Try different possible paths
        let possiblePaths = [
            "Data/Resources/Protocol",  // Asset catalog path
            "Data/Raw/pdf",           // Raw PDFs path
            ""                        // Root path
        ]
        
        let cleanFileName = pdfUrlString.replacingOccurrences(of: ".pdf", with: "")
        
        for path in possiblePaths {
            let filePath = path.isEmpty ? cleanFileName : "\(path)/\(cleanFileName)"
            if let url = Bundle.main.url(forResource: filePath, withExtension: "pdf") {
                pdfURL = url
                return
            }
        }
        
        // If we get here, we couldn't find the PDF
        errorMessage = "PDF not found: \(pdfUrlString)"
        print("⚠️ \(errorMessage ?? "")")
        print("Bundle paths searched:")
        for path in possiblePaths {
            print("- \(path)")
        }
    }
}

struct PDFKitView: UIViewRepresentable {
    let url: URL
    
    func makeUIView(context: Context) -> PDFView {
        let pdfView = PDFView()
        pdfView.document = PDFDocument(url: self.url)
        pdfView.autoScales = true
        return pdfView
    }
    
    func updateUIView(_ pdfView: PDFView, context: Context) {
        // Update pdf if needed
    }
}

#Preview(windowStyle: .automatic) {
    PDFContentView("")
}
