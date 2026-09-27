# Third-party notices

FocusTone application code is MIT licensed. Third-party libraries retain their own licenses.

| Library | Version | Copyright / license |
|---|---|---|
| NAudio and its component packages | 2.2.1 | Mark Heath and contributors; MIT |
| NAudio.Vorbis | 1.5.0 | Andrew Ward; MIT |
| NVorbis | 0.10.4 | Andrew Ward; MIT |
| TagLibSharp | 2.3.0 | Brian Nickel, Gabriel Burt, Stephen Shaw and contributors; LGPL-2.1-only |
| Microsoft.Web.WebView2 SDK | 1.0.4191.47 | Microsoft; accompanying Microsoft SDK license |
| .NET / Windows Desktop Runtime | publish-selected .NET 10 | Microsoft and contributors; accompanying runtime notices |
| Microsoft.Win32.Registry, System.Security.AccessControl, System.Security.Principal.Windows | transitive dependencies | Microsoft; MIT |

Release archives include `licenses/` and the unchanged TagLibSharp 2.3.0 source archive in `third-party-source/`. TagLibSharp is a separate dynamically loaded DLL. You may build and substitute a modified library, including for debugging such modifications. The complete FocusTone source and build scripts are available in the same repository. No restriction on reverse engineering for debugging modifications to the LGPL-covered library is imposed by FocusTone.

Source: https://github.com/mono/taglib-sharp/tree/TaglibSharp-2.3.0.0

The proprietary WebView2 SDK is redistributable under its Microsoft license; it is not described as MIT. The Evergreen Runtime is installed/updated by Microsoft separately. The application has no association with, endorsement by or sponsorship from Bilibili, Microsoft, or any game publisher.

No commercial music, game asset, video or cover artwork is included. The optional test-tone generation script creates an original synthetic tone locally. A software license does not license imported media.
