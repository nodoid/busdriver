# Bus Driver — release 1.0

Everything needed to submit Bus Driver to the App Store and Google Play, plus
the source builds they were produced from.

```
releases/
  android/uk.co.allthejohnsons.busdriver-1.0.aab   Android App Bundle for Play
  ios/BusDriver-1.0-ios-arm64.ipa              iOS AOT build, signed for distribution
                                               (built locally, not in git - see below)
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

# iOS device build, AOT, signed for distribution (ios-arm64 -> .ipa)
dotnet publish src/BusDriver.iOS/BusDriver.iOS.csproj \
  -c Release -f net10.0-ios -p:RuntimeIdentifier=ios-arm64 -p:BuildIpa=true \
  -p:CodesignKey="Apple Distribution: <team>" \
  -p:CodesignProvision="<profile name>"

# iOS simulator build, used for the screenshots and the gameplay video
dotnet build src/BusDriver.iOS/BusDriver.iOS.csproj -f net10.0-ios \
  -p:RuntimeIdentifier=iossimulator-arm64 -c Debug
```

## The iOS build

`releases/ios/BusDriver-1.0-ios-arm64.ipa` is the submission artefact: a full
AOT (`ios-arm64`) Release build, signed with the Apple Distribution identity and
the `rel-busdriver` App Store provisioning profile, bundle id
`uk.co.allthejohnsons.busdriver`, version 1.0 (1), with `get-task-allow` false
and `beta-reports-active` true. Upload it with Transporter or Xcode.

It is not committed: the AOT payload takes the file to ~104 MB, over GitHub's
per-file limit, and signing it embeds the publisher's certificate and profile.
The tracked sources plus the build command above reproduce it byte-for-byte on
the publishing machine - the identity and profile never enter the repository.

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
