import ARKit
import AVKit
import SwiftUI
import RealityKit

import Foundation
import CoreGraphics
import MetalKit
import Accelerate

var currentTexture: MTLTexture?
let mtlDevice: MTLDevice = MTLCreateSystemDefaultDevice()!
var textureCache: CVMetalTextureCache! = nil
var commandQueue: MTLCommandQueue!
var pointer: UnsafeMutableRawPointer! = nil

var isRunning: Bool = false
let arKitSession = ARKitSession()

var updateCount: UInt64 = 0

@_cdecl("startCapture")
public func startCapture() 
{
    print("############ START ############")
    
    isRunning = true
    
    Task 
    {
        // Create a format for the main camera
        let formats = CameraVideoFormat.supportedVideoFormats(for: .main, cameraPositions: [.left])
        
        // Create an ARKitSession and request camera access
        // let arKitSession = ARKitSession()
        let status = await arKitSession.queryAuthorization(for: [.cameraAccess])
        print("Query Authorization Status :", status)

        // Run a provider to get camera frames in ARKitSession
        let cameraFrameProvider = CameraFrameProvider()
        do 
        {
            try await arKitSession.run([cameraFrameProvider])
        }
        catch 
        {
            print("ARKit Session Failed:", error)
            return
        }
        
        print("Running ARKit Session.")

        // If the camera frame is updated here, the PixelBuffer can be obtained.
        for await cameraFrameUpdate in cameraFrameProvider.cameraFrameUpdates(for:  formats[0])! 
        {
            if !isRunning 
            {
                break
            }
            
            createTexture(cameraFrameUpdate.primarySample.pixelBuffer)
        }
    }
}

@_cdecl("stopCapture")
public func stopCapture() 
{
    print("############ STOP ##############")
    
    isRunning = false
    
    arKitSession.stop()
}

@_cdecl("getTexture")
public func getTexture() -> UnsafeMutableRawPointer? 
{
    return pointer
}

@_cdecl("getUpdateCount")
public func getUpdateCount() -> UInt64
{
    return updateCount
}

// Sending a texture from Swift Native to Unity
// https://qiita.com/fuziki/items/2b4ad38c36afeb16e96a
// Convert the pixel buffer (CVPixelBuffer) received from the camera to an MTLTexture
private func createTexture(_ pixelBuffer: CVPixelBuffer)
{
    // The acquired CVPixelBuffer is in YUV format, so it will be converted to BGRA format before processing. (The conversion process will be described later.)
    guard let pixelBufferBGRA: CVPixelBuffer = try? pixelBuffer.toBGRA() else
    {
        print("Error: Failed to convert pixel buffer to BGRA.")
        return
    }
    
    let width = CVPixelBufferGetWidth(pixelBufferBGRA)
    let height = CVPixelBufferGetHeight(pixelBufferBGRA)
    
    // print("Width: \(width), Height: \(height)")
    
    // Prepare with the classes needed to create an MTLTexture
    var cvTexture: CVMetalTexture?
    if textureCache == nil 
    {
        CVMetalTextureCacheCreate(kCFAllocatorDefault, nil, mtlDevice, nil, &textureCache)
    }
    
    _ = CVMetalTextureCacheCreateTextureFromImage(kCFAllocatorDefault,
                                                  textureCache,
                                                  pixelBufferBGRA,
                                                  nil,
                                                  .bgra8Unorm_srgb,
                                                  width,
                                                  height,
                                                  0,
                                                  &cvTexture)
    
    guard let imageTexture = cvTexture else { return }
    
    // Extract the MTLTexture from the created CVMetalTexture
    let texture: MTLTexture = CVMetalTextureGetTexture(imageTexture)!
    
    // Create a currentTexture here (if necessary)
    // From now on, a reference to this currentTexture will be passed to Unity
    if currentTexture == nil
    {
        let texdescriptor = MTLTextureDescriptor
            .texture2DDescriptor(pixelFormat: texture.pixelFormat,
                                 width: texture.width,
                                 height: texture.height,
                                 mipmapped: false)
        texdescriptor.usage = .unknown
        currentTexture = mtlDevice.makeTexture(descriptor: texdescriptor)
    }
    
    // Copy the camera data to the currentTexture using the CommandBuffer
    if commandQueue == nil
    {
        commandQueue = mtlDevice.makeCommandQueue()
    }
    
    let commandBuffer = commandQueue.makeCommandBuffer()!
    let blitEncoder = commandBuffer.makeBlitCommandEncoder()!
    
    blitEncoder.copy(from: texture,
                     sourceSlice: 0, sourceLevel: 0,
                     sourceOrigin: MTLOrigin(x: 0, y: 0, z: 0),
                     sourceSize: MTLSizeMake(texture.width, texture.height, texture.depth),
                     to: currentTexture!, destinationSlice: 0, destinationLevel: 0,
                     destinationOrigin: MTLOrigin(x: 0, y: 0, z: 0))
    blitEncoder.endEncoding()
    commandBuffer.commit()
    commandBuffer.waitUntilCompleted()
    
    OSAtomicIncrement64(&updateCount)
    
    // Keep a reference to it as an opaque pointer to pass to C#. This is the pointer that C# actually accesses.
    if pointer == nil
    {
        pointer = Unmanaged.passUnretained(currentTexture!).toOpaque()
    }
}

// ----------------------------------------------------
// Conversion from YUV to BGRA
// https://qiita.com/noppefoxwolf/items/b12d56e052664a21d8b6
extension CVPixelBuffer
{
    public func toBGRA() throws -> CVPixelBuffer? 
    {
        let pixelBuffer = self

        /// Check format
        let pixelFormat = CVPixelBufferGetPixelFormatType(pixelBuffer)
        guard pixelFormat == kCVPixelFormatType_420YpCbCr8BiPlanarFullRange else { return pixelBuffer }

        /// Split plane
        let yImage: VImage = pixelBuffer.with({ VImage(pixelBuffer: $0, plane: 0) })!
        let cbcrImage: VImage = pixelBuffer.with({ VImage(pixelBuffer: $0, plane: 1) })!

        /// Create output pixelBuffer
        let outPixelBuffer = CVPixelBuffer.make(width: yImage.width, height: yImage.height, format: kCVPixelFormatType_32BGRA)!

        /// Convert yuv to argb
        var argbImage = outPixelBuffer.with({ VImage(pixelBuffer: $0) })!
        try argbImage.draw(yBuffer: yImage.buffer, cbcrBuffer: cbcrImage.buffer)
        /// Convert argb to bgra
        argbImage.permute(channelMap: [3, 2, 1, 0])

        return outPixelBuffer
    }
}

struct VImage 
{
    let width: Int
    let height: Int
    let bytesPerRow: Int
    var buffer: vImage_Buffer

    init?(pixelBuffer: CVPixelBuffer, plane: Int) 
    {
        guard let rawBuffer = CVPixelBufferGetBaseAddressOfPlane(pixelBuffer, plane) else { return nil }
        self.width = CVPixelBufferGetWidthOfPlane(pixelBuffer, plane)
        self.height = CVPixelBufferGetHeightOfPlane(pixelBuffer, plane)
        self.bytesPerRow = CVPixelBufferGetBytesPerRowOfPlane(pixelBuffer, plane)
        self.buffer = vImage_Buffer(
            data: UnsafeMutableRawPointer(mutating: rawBuffer),
            height: vImagePixelCount(height),
            width: vImagePixelCount(width),
            rowBytes: bytesPerRow
        )
    }

    init?(pixelBuffer: CVPixelBuffer) 
    {
        guard let rawBuffer = CVPixelBufferGetBaseAddress(pixelBuffer) else { return nil }
        self.width = CVPixelBufferGetWidth(pixelBuffer)
        self.height = CVPixelBufferGetHeight(pixelBuffer)
        self.bytesPerRow = CVPixelBufferGetBytesPerRow(pixelBuffer)
        self.buffer = vImage_Buffer(
            data: UnsafeMutableRawPointer(mutating: rawBuffer),
            height: vImagePixelCount(height),
            width: vImagePixelCount(width),
            rowBytes: bytesPerRow
        )
    }

    mutating func draw(yBuffer: vImage_Buffer, cbcrBuffer: vImage_Buffer) throws 
    {
        try buffer.draw(yBuffer: yBuffer, cbcrBuffer: cbcrBuffer)
    }

    mutating func permute(channelMap: [UInt8]) {
        buffer.permute(channelMap: channelMap)
    }
}

extension CVPixelBuffer 
{
    func with<T>(_ closure: ((_ pixelBuffer: CVPixelBuffer) -> T)) -> T 
    {
        CVPixelBufferLockBaseAddress(self, .readOnly)
        let result = closure(self)
        CVPixelBufferUnlockBaseAddress(self, .readOnly)
        return result
    }

    static func make(width: Int, height: Int, format: OSType) -> CVPixelBuffer? 
    {
        var pixelBuffer: CVPixelBuffer? = nil
        CVPixelBufferCreate(kCFAllocatorDefault,
                            width,
                            height,
                            format,
                            [String(kCVPixelBufferIOSurfacePropertiesKey): [
                                "IOSurfaceOpenGLESFBOCompatibility": true,
                                "IOSurfaceOpenGLESTextureCompatibility": true,
                                "IOSurfaceCoreAnimationCompatibility": true,
                            ]] as CFDictionary,
                            &pixelBuffer)
        return pixelBuffer
    }
}

extension vImage_Buffer 
{
    mutating func draw(yBuffer: vImage_Buffer, cbcrBuffer: vImage_Buffer) throws 
    {
        var yBuffer = yBuffer
        var cbcrBuffer = cbcrBuffer
        var conversionMatrix: vImage_YpCbCrToARGB = {
            var pixelRange = vImage_YpCbCrPixelRange(Yp_bias: 0, CbCr_bias: 128, YpRangeMax: 255, CbCrRangeMax: 255, YpMax: 255, YpMin: 1, CbCrMax: 255, CbCrMin: 0)
            var matrix = vImage_YpCbCrToARGB()
            vImageConvert_YpCbCrToARGB_GenerateConversion(kvImage_YpCbCrToARGBMatrix_ITU_R_709_2, &pixelRange, &matrix, kvImage420Yp8_CbCr8, kvImageARGB8888, UInt32(kvImageNoFlags))
            return matrix
        }()
        let error = vImageConvert_420Yp8_CbCr8ToARGB8888(&yBuffer, &cbcrBuffer, &self, &conversionMatrix, nil, 255, UInt32(kvImageNoFlags))
        if error != kvImageNoError {
            fatalError()
        }
    }

    mutating func permute(channelMap: [UInt8]) 
    {
        vImagePermuteChannels_ARGB8888(&self, &self, channelMap, 0)
    }
}
