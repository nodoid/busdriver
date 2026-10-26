# Bus Driver — release 1.0

Everything needed to submit Bus Driver to the App Store and Google Play, plus
the source builds they were produced from.

```
releases/
  android/uk.co.scarthorpe.busdriver-1.0.aab   Android App Bundle for Play
  ios/BusDriver-1.0-ios-arm64-unsigned.ipa     iOS release build (sign before upload)
  screenshots/ios-6.9-inch/                    2868 x 1320 (App Store, 6.9-inch)
  screenshots/android-phone/                   1920 x 1080 (Play, phone)
  screenshots/feature-graphic.png              1024 x 500 (Play)
  screenshots/play-icon-512.png                512 x 512 (Play)
  store/app-store-listing.md                   App Store Connect copy and notes
  store/google-play-listing.md                 Play Console copy and notes
  store/privacy-policy.md                      Privacy policy for both stores
```

The gameplay video is kept out of the repository and lives at
`~/Movies/BusDriver - iOS gameplay (iPhone 17 Pro Max).mp4` (13.9 s, 2868 x 1320,
H.264). The support and privacy page is at `~/Downloads/busdriver-support.html`.

## Build commands

```sh
# Android App Bundle (signed with the local debug key; Play App Signing
# re-signs it on upload)
dotnet publish src/BusDriver.Android/BusDriver.Android.csproj \
  -c Release -f net10.0-android -p:AndroidPackageFormat=aab

# iOS device build without signing (AOT, ios-arm64)
dotnet publish src/BusDriver.iOS/BusDriver.iOS.csproj \
  -c Release -f net10.0-ios -p:RuntimeIdentifier=ios-arm64 \
  -p:EnableCodeSigning=false -p:BuildIpa=false

# iOS simulator build, used for the screenshots and the gameplay video
dotnet build src/BusDriver.iOS/BusDriver.iOS.csproj -f net10.0-ios \
  -p:RuntimeIdentifier=iossimulator-arm64 -c Debug
```

## Signing the iOS build

The repository deliberately contains no developer identity, team identifier or
provisioning profile. To produce an App Store build, sign the app in Xcode with
the publisher's own team (or pass the identity and profile to `dotnet publish`):

```sh
dotnet publish src/BusDriver.iOS/BusDriver.iOS.csproj \
  -c Release -f net10.0-ios -p:RuntimeIdentifier=ios-arm64 -p:BuildIpa=true \
  -p:CodesignKey="Apple Distribution: <team>" \
  -p:CodesignProvision="<profile name>"
```

The unsigned `.ipa` in this directory is the exact Release code packaged for
the device (`ios-arm64`), built without signing and with the MonoGame/JIT
interpreter enabled so it could be produced on a machine with no provisioning
profile. App Store submission needs an AOT build signed with a distribution
profile, so rebuild through Xcode or the `dotnet publish` command above on the
publishing machine, then upload with Transporter or Xcode.

The Android bundle in `releases/android/` is signed with the local debug key;
Play App Signing re-signs it on upload.

## Capture hooks

The game reads a few environment variables that only exist to make marketing
capture possible without a second pair of hands. They do nothing unless set:

| Variable | Effect |
| --- | --- |
| `BUSDRIVER_SCREEN` | Opens on a named screen (`Title`, `Fleet`, `Difficulty`, `Routes`, `Help`, `Playing`) |
| `BUSDRIVER_AUTOSTART=1` | Starts a shift immediately |
| `BUSDRIVER_ROUTE` | Route number for the autostart (`2`, `7`, `1A`, …) |
| `BUSDRIVER_LEVEL` | `Easy`, `Medium` or `Hard` |
| `BUSDRIVER_CRUISE=1` | Latches the throttle at the start of the autostarted shift |

With the iOS simulator these are passed as `SIMCTL_CHILD_BUSDRIVER_*`.

## Version

- Version 1.0, build 1
- iOS minimum: 18.0, landscape only
- Android minimum: API 32, `sensorLandscape`
- No permissions, no network access, no analytics, no advertising
