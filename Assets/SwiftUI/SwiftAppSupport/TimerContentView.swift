import SwiftUI
import AVFoundation
import AudioToolbox

struct TimerContentView: View {
    @State private var timeRemaining: Int
    @State private var totalTime: Int
    @State private var timer: Timer?
    @State private var isActive = false
    @State private var showingSetTimerView = false
    @State private var isAlarmPlaying = false
    
    private var alarmSoundID: SystemSoundID = 1005 // This is the ID for the "Alarm" system sound
    
    init(_ duration: Int?) {
        _timeRemaining = State(initialValue: duration ?? 0)
        _totalTime = State(initialValue: duration ?? 0)
    }

    var body: some View {
        VStack {
            ZStack {
                Circle()
                    .stroke(lineWidth: 20)
                    .opacity(0.3)
                    .foregroundColor(.gray)
                
                Circle()
                    .trim(from: 0.0, to: CGFloat(timeRemaining) / CGFloat(max(totalTime, 1)))
                    .stroke(style: StrokeStyle(lineWidth: 20, lineCap: .round, lineJoin: .round))
                    .foregroundColor(.blue)
                    .rotationEffect(Angle(degrees: 270.0))
                    .animation(.linear, value: timeRemaining)
                
                Text(timeString(from: timeRemaining))
                    .font(.largeTitle)
                    .padding()
            }
            .frame(width: 200, height: 200)
            .padding()
            
            if isActive {
                Button("Stop") {
                    stopTimer()
                }
            } else {
                HStack {
                    Button("Start") {
                        startTimer()
                    }
                    .disabled(timeRemaining == 0)
                    
                    Button("Set Timer") {
                        showingSetTimerView = true
                    }
                }
            }
            
            if isAlarmPlaying {
                Button("Dismiss Alarm") {
                    dismissAlarm()
                }
                .background(Color.red)
                .foregroundColor(.white)
                .cornerRadius(10)
            }
        }
        .sheet(isPresented: $showingSetTimerView) {
            SetTimerView(onSet: { hours, minutes, seconds in
                setDuration(hours * 3600 + minutes * 60 + seconds)
                showingSetTimerView = false
            })
        }
    }
    
    func timeString(from seconds: Int) -> String {
        let hours = seconds / 3600
        let minutes = (seconds % 3600) / 60
        let remainingSeconds = seconds % 60
        return String(format: "%02d:%02d:%02d", hours, minutes, remainingSeconds)
    }
    
    func startTimer() {
        isActive = true
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { _ in
            if timeRemaining > 0 {
                timeRemaining -= 1
            } else {
                stopTimer()
                playAlarm()
            }
        }
    }
    
    func stopTimer() {
        isActive = false
        timer?.invalidate()
        timer = nil
    }
    
    func setDuration(_ seconds: Int) {
        timeRemaining = seconds
        totalTime = seconds
    }
    
    func playAlarm() {
        isAlarmPlaying = true
        AudioServicesPlaySystemSound(alarmSoundID)
    }
    
    func dismissAlarm() {
        isAlarmPlaying = false
        AudioServicesDisposeSystemSoundID(alarmSoundID)
    }
}

struct SetTimerView: View {
    @State private var hours: Int = 0
    @State private var minutes: Int = 5
    @State private var seconds: Int = 0
    var onSet: (Int, Int, Int) -> Void
    
    var body: some View {
        VStack(spacing: 20) {
            HStack {
                Picker("Hours", selection: $hours) {
                    ForEach(0...23, id: \.self) { hour in
                        Text("\(hour) h").tag(hour)
                    }
                }
                .pickerStyle(WheelPickerStyle())
                
                Picker("Minutes", selection: $minutes) {
                    ForEach(0...59, id: \.self) { minute in
                        Text("\(minute) m").tag(minute)
                    }
                }
                .pickerStyle(WheelPickerStyle())
                
                Picker("Seconds", selection: $seconds) {
                    ForEach(0...59, id: \.self) { second in
                        Text("\(second) s").tag(second)
                    }
                }
                .pickerStyle(WheelPickerStyle())
            }
            .padding()
            
            Button("Set") {
                onSet(hours, minutes, seconds)
            }
        }
        .padding()
    }
}

#Preview(windowStyle: .automatic) {
    TimerContentView(300)
}