# MAUI Android Setup

Last updated: 2026-06-25

`Velonixs.Connect.Portal.Maui` is an Android-only MAUI Blazor Hybrid shell targeting `net10.0-android`. It reuses the typed API client, shared portal Razor components, and `Velonixs.Connect.Mobile.Abstractions`.

## Local Toolchain

The current workstation was prepared with:

```powershell
dotnet workload install maui-android
winget install Microsoft.OpenJDK.17 --accept-package-agreements --accept-source-agreements --silent
dotnet build src/Velonixs.Connect.Portal.Maui/Velonixs.Connect.Portal.Maui.csproj -f net10.0-android -t:InstallAndroidDependencies -p:AndroidSdkDirectory="$env:LOCALAPPDATA\Android\Sdk" -p:JavaSdkDirectory="C:\Program Files\Microsoft\jdk-17.0.19.10-hotspot" -p:AcceptAndroidSDKLicenses=True
```

For command-line builds, these environment variables should be available:

```powershell
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"
$env:ANDROID_SDK_ROOT = "$env:LOCALAPPDATA\Android\Sdk"
$env:JAVA_HOME = "C:\Program Files\Microsoft\jdk-17.0.19.10-hotspot"
```

The variables were also persisted with `setx` on this workstation. Open a new terminal if a build still cannot find Java or the Android SDK.

## Build

Build the Android shell:

```powershell
dotnet build src/Velonixs.Connect.Portal.Maui/Velonixs.Connect.Portal.Maui.csproj -f net10.0-android
```

Build the full solution after the Android environment variables are available:

```powershell
dotnet build Velonixs.Connect.sln --no-restore
```

## Debug APK for Phone Verification

The latest local verification APK was built on this workstation with:

```powershell
dotnet publish src/Velonixs.Connect.Portal.Maui/Velonixs.Connect.Portal.Maui.csproj `
  -f net10.0-android `
  -c Debug `
  -p:AndroidPackageFormat=apk `
  -p:ApiBaseAddress="http://192.168.1.2:5077/"
```

The generated APK was copied to:

```text
artifacts/android/velonixs-connect-portal-debug.apk
```

Current APK SHA-256:

```text
AF6308C5308D45AECBFDC33F0423B8CB0082B13C23C5E7C59B6DF852CB98914A
```

The Debug APK is built with fast deployment disabled and assemblies embedded. This is required for side-loading; otherwise Android may crash with a message like `No assemblies found in ... .__override__`.

This Debug APK is signed for side-loading and is intended for local verification only. Rebuild it whenever the API host address changes:

```powershell
dotnet publish src/Velonixs.Connect.Portal.Maui/Velonixs.Connect.Portal.Maui.csproj `
  -f net10.0-android `
  -c Debug `
  -p:AndroidPackageFormat=apk `
  -p:ApiBaseAddress="http://YOUR-PC-LAN-IP:5077/"
```

## Local API Addressing

The MAUI shell reads its API base address from the `ApiBaseAddress` MSBuild property. If the property is omitted, it defaults to the Android emulator host loopback alias:

```text
https://10.0.2.2:7011/
```

For a physical device, use a reachable LAN host, reverse proxy, or dev tunnel with a trusted certificate. Local ASP.NET Core development certificates are not automatically trusted by Android devices.

For the local Debug APK above, start the API on all network interfaces over HTTP:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/Velonixs.Connect.Api/Velonixs.Connect.Api.csproj --no-launch-profile --urls "http://0.0.0.0:5077"
```

Then verify from the phone browser before opening the app:

```text
http://192.168.1.2:5077/swagger
```

If the phone cannot load Swagger:

- Confirm the phone and workstation are on the same Wi-Fi network.
- Confirm the workstation IP has not changed with `Get-NetIPAddress -AddressFamily IPv4`.
- Allow inbound TCP traffic on port `5077` through Windows Defender Firewall.
- Rebuild the APK with the updated `ApiBaseAddress` if the IP changes.

The Android manifest allows cleartext HTTP so this local LAN APK can talk to the development API. Use HTTPS or a trusted tunnel for production builds.

## If the App Does Not Open

The APK package metadata should show:

- Package: `com.velonixs.connect.portal`
- Minimum Android version: Android 7.0/API 24
- Launchable activity: `MainActivity`

If tapping the app immediately closes it, connect the phone with USB debugging and collect logs:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" devices
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" logcat -c
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" shell monkey -p com.velonixs.connect.portal 1
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" logcat -d -v time | Select-String -Pattern "com.velonixs.connect.portal|AndroidRuntime|DOTNET|FATAL EXCEPTION"
```

If `adb devices` returns an empty list, enable Developer options and USB debugging on the phone, reconnect the USB cable, and approve the debugging prompt on the phone.

If logs show the app is running but `NotificationShade` or lock screen has focus, unlock the phone manually. `dumpsys window` may show `mDreamingLockscreen=true` while the app is already running behind the lock screen.

## Install on a Physical Phone

Prerequisites:

- Android 7.0/API 24 or newer.
- Same Wi-Fi network as the development workstation, unless using a public tunnel.
- The API running on a reachable address, for example `http://192.168.1.2:5077/`.
- Side-loading enabled on the phone, usually `Install unknown apps` for the file manager/browser used to open the APK.
- Optional: USB debugging and Android platform tools if installing with `adb`.

Install options:

```powershell
adb install -r artifacts/android/velonixs-connect-portal-debug.apk
```

On this workstation, `adb` is available from the Android SDK even if it is not on `PATH`:

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe" install -r artifacts/android/velonixs-connect-portal-debug.apk
```

Or transfer `artifacts/android/velonixs-connect-portal-debug.apk` to the phone and open it from the phone's file manager.

After install:

- Open `Velonixs Restaurant Portal`.
- Log in with a restaurant staff/admin account from the API database.
- Approve notification permission when prompted on Android 13+.
- Create or update an order from another client and confirm dashboard/order screens refresh through SignalR.
- Verify a new order raises the local notification/alert behavior.

## Android Signing

Do not commit keystores, passwords, `.aab`, or `.apk` artifacts.

Local signing inputs:

- `VELONIXS_ANDROID_KEYSTORE`
- `VELONIXS_ANDROID_KEYSTORE_PASSWORD`
- `VELONIXS_ANDROID_KEY_ALIAS`
- `VELONIXS_ANDROID_KEY_PASSWORD`

The release pipeline should inject these as secrets and produce an Android App Bundle:

```powershell
dotnet publish src/Velonixs.Connect.Portal.Maui/Velonixs.Connect.Portal.Maui.csproj -f net10.0-android -c Release
```

## GitHub Release Workflow

`.github/workflows/android-maui-release.yml` builds the Android app bundle on `workflow_dispatch` and when mobile-related files change on `master`.

Required repository secrets:

- `VELONIXS_ANDROID_KEYSTORE_BASE64`: base64-encoded release keystore file.
- `VELONIXS_ANDROID_KEYSTORE_PASSWORD`
- `VELONIXS_ANDROID_KEY_ALIAS`
- `VELONIXS_ANDROID_KEY_PASSWORD`

The workflow decodes the keystore into the runner temp directory, publishes `Velonixs.Connect.Portal.Maui` in `Release`, and uploads the `.aab` as the `velonixs-portal-android-aab` artifact.

## Current Mobile Coverage

- Login with secure token persistence through `SecureStorage.Default`.
- Dashboard summary with shared metric tiles and connectivity state.
- Order queue and order detail with confirm/reject actions guarded while offline.
- Menu availability updates guarded while offline.
- Customer editing.
- Staff account creation and activation/deactivation guarded while offline.
- Notification/message log review.
- Restaurant tax settings and ordering availability guarded while offline.
- Android local notification permission and notification channel creation through `AndroidLocalNotificationService`.
- App lifecycle state abstraction wired from MAUI app sleep/resume.

## Verification Checklist

- Login persists across app restarts through secure storage.
- Logout clears secure storage and returns to login.
- API refresh-token retry works after access-token expiry.
- Offline state is visible and disables order-changing actions.
- New-order local notification is displayed from SignalR order events on an attached Android device.
- New-order alert feedback respects Android notification/audio settings on an attached Android device.
- App resumes cleanly and refreshes dashboard/order lists.

Device validation still requires an attached Android emulator or physical device. `adb devices` currently returns no connected devices on this workstation.
