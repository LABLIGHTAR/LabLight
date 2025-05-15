// /*
// using Unity.VisualScripting;
// using UnityEngine;

// public class ServiceSetup : MonoBehaviour
// {
//     // private IFrameProvider frameProvider;

//     private void Awake()
//     {
//         /*
//         frameProvider = CreateFrameProvider();
//         frameProvider.Initialize(1920, 1080);
//         frameProvider.Start();        
//         ServiceRegistry.RegisterService<IFrameProvider>(frameProvider);
//         */
//     }

//     private void OnDestroy()
//     {
//         /*
//         frameProvider.Stop();
//         ServiceRegistry.UnRegisterService<IFrameProvider>();
//         */
//     }

//     public static IFrameProvider CreateFrameProvider()
//     {
//         /*
// #if UNITY_STANDALONE || UNITY_EDITOR
//         return new StandaloneWebCamFrameProvider();
// #elif UNITY_VISIONOS && !UNITY_EDITOR
//         //return new VisionOSFrameProvider();
// #else
//         throw new NotSupportedException("No frame provider available for this platform");
// #endif
//         */
//         return null; // Added to avoid compilation error, will be removed if class is removed
//     }
// }
// */
