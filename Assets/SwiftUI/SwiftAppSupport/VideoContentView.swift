import SwiftUI
import AVKit

struct VideoContentView: View {
    let videoUrlString: String
    @State private var isPlaying: Bool = false
    @State private var player: AVPlayer?
    @State private var playerObserver: Any?
    @State private var isPresentingFullScreenPlayer = false
    @State private var errorMessage: String?

    init(_ videoUrlString: String) {
        self.videoUrlString = videoUrlString
    }

    var body: some View {
        VStack {
            if let player = player {
                SpatialVideoPlayerView(player: player)
                    .frame(width: 900, height: 600)
                    .background(Color.black)
                    .cornerRadius(10)
                    .shadow(radius: 10)
                    .onAppear {
                        setupPlayerObserver()
                    }
                    .onDisappear {
                        removePlayerObserver()
                    }

                // Playback controls
                HStack {
                    Button(action: {
                        let currentTime = player.currentTime()
                        let newTime = CMTimeMake(value: currentTime.value - 10 * Int64(currentTime.timescale), timescale: currentTime.timescale)
                        player.seek(to: newTime)
                    }) {
                        Image(systemName: "gobackward.10")
                            .foregroundColor(.white)
                            .padding()
                    }
                    
                    Button(action: {
                        if isPlaying {
                            player.pause()
                        } else {
                            player.play()
                        }
                        isPlaying.toggle()
                    }) {
                        Image(systemName: isPlaying ? "pause.fill" : "play.fill")
                            .foregroundColor(.white)
                            .padding()
                    }
                    .padding()

                    Button(action: {
                        let currentTime = player.currentTime()
                        let newTime = CMTimeMake(value: currentTime.value + 10 * Int64(currentTime.timescale), timescale: currentTime.timescale)
                        player.seek(to: newTime)
                    }) {
                        Image(systemName: "goforward.10")
                            .foregroundColor(.white)
                            .padding()
                    }
                    // Button to present fullscreen player
                    Button(action: {
                        isPresentingFullScreenPlayer = true
                    }) {
                        Text("View Immersive")
                            .padding()
                            .cornerRadius(10)
                    }
                    .sheet(isPresented: $isPresentingFullScreenPlayer) {
                        // Present AVPlayerViewController in fullscreen mode
                        AVPlayerViewControllerWrapper(player: player)
                    }  
                }
            } else if let errorMessage = errorMessage {
                VStack {
                    Image(systemName: "video.slash")
                        .font(.largeTitle)
                        .foregroundColor(.red)
                    Text(errorMessage)
                        .foregroundColor(.red)
                        .padding()
                }
                .frame(height: 200)
                .frame(maxWidth: .infinity)
                .background(Color(.systemGray6))
                .cornerRadius(8)
            } else {
                ProgressView()
                    .padding()
            }
        }
        .onAppear {
            setupPlayer()
        }
    }

    private func setupPlayer() {
        // Try different file locations and extensions
        let possibleExtensions = ["mp4", "mov", "m4v", "MOV", "MP4"]
        let possiblePaths = [
            "Data/Resources/Protocol",  // Asset catalog path
            "Data/Raw/videos",         // Raw videos path
            ""                         // Root path
        ]
        
        // First try remote URL
        if let url = URL(string: videoUrlString), UIApplication.shared.canOpenURL(url) {
            player = AVPlayer(url: url)
            return
        }
        
        // Then try local files
        let cleanFileName = videoUrlString.replacingOccurrences(of: ".mp4", with: "")
                                        .replacingOccurrences(of: ".mov", with: "")
                                        .replacingOccurrences(of: ".m4v", with: "")
        
        for path in possiblePaths {
            for ext in possibleExtensions {
                let filePath = path.isEmpty ? cleanFileName : "\(path)/\(cleanFileName)"
                if let url = Bundle.main.url(forResource: filePath, withExtension: ext) {
                    player = AVPlayer(url: url)
                    return
                }
            }
        }
        
        // If we get here, we couldn't find the video
        errorMessage = "Video not found: \(videoUrlString)"
        print("⚠️ \(errorMessage ?? "")")
        print("Bundle paths searched:")
        for path in possiblePaths {
            print("- \(path)")
        }
    }

    private func setupPlayerObserver() {
        guard let player = player else { return }
        playerObserver = player.addPeriodicTimeObserver(forInterval: CMTime(seconds: 1, preferredTimescale: 600), queue: .main) { _ in
            if player.currentItem?.status == .readyToPlay {
                isPlaying = player.rate != 0
            }
        }
    }

    private func removePlayerObserver() {
        if let observer = playerObserver, let player = player {
            player.removeTimeObserver(observer)
            playerObserver = nil
        }
    }

    class CustomPlayerView: UIView {
        private let playerLayer: AVPlayerLayer

        init(player: AVPlayer) {
            self.playerLayer = AVPlayerLayer(player: player)
            super.init(frame: .zero)
            setupLayer()
        }

        required init?(coder: NSCoder) {
            fatalError("init(coder:) has not been implemented")
        }

        private func setupLayer() {
            playerLayer.videoGravity = .resizeAspect
            layer.addSublayer(playerLayer)
        }

        override func layoutSubviews() {
            super.layoutSubviews()
            playerLayer.frame = bounds
        }
    }

    struct SpatialVideoPlayerView: UIViewRepresentable {
        let player: AVPlayer

        func makeUIView(context: Context) -> CustomPlayerView {
            let view = CustomPlayerView(player: player)
            view.backgroundColor = .black
            return view
        }

        func updateUIView(_ uiView: CustomPlayerView, context: Context) {
            uiView.layoutSubviews() // Force layout update
        }
    }
    
    struct AVPlayerViewControllerWrapper: UIViewControllerRepresentable {
        let player: AVPlayer
        
        func makeUIViewController(context: Context) -> AVPlayerViewController {
            let playerViewController = AVPlayerViewController()
            playerViewController.player = player
            return playerViewController
        }
        
        func updateUIViewController(_ uiViewController: AVPlayerViewController, context: Context) {
            // Update if needed
        }
    }
}
