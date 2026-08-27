# Third-party notices

## NativeGallery for Unity
- Author: Süleyman Yasir Kula (yasirkula)
- Source: https://github.com/yasirkula/UnityNativeGallery
- Version: 1.9.4
- License: MIT
- Used for: Android gallery image picking (F1 image acquisition seam,
  `ImageAcquisitionAndroid`).
- Import method: import `NativeGallery.unitypackage` from the official v1.9.4
  GitHub release. The imported runtime files live under
  `Assets/Plugins/NativeGallery/`.
- Android integration: the imported `NativeGallery.aar` and runtime code own
  picker and permission handling; TraceJournal adds no duplicate permission
  request. The import does not change `Packages/manifest.json` or
  `Packages/packages-lock.json`.

Full MIT license text is included in the imported package under
`Assets/Plugins/NativeGallery/LICENSE.txt`; do not remove it.
