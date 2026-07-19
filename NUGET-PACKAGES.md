# NuGet Packages

Packages published from this repository to the local NuGet feed.

<!-- Managed by the nuget-publish skill. The publish script parses and updates the
     tables below - keep their column layout intact. -->

## Packages

| Package ID | Project | Current Version |
| --- | --- | --- |
| MeshTransit.Contracts | src/MeshTransit.Contracts/MeshTransit.Contracts.csproj | 0.2.0 |
| MeshTransit | src/MeshTransit/MeshTransit.csproj | 0.2.0 |
| MeshTransit.Client | src/MeshTransit.Client/MeshTransit.Client.csproj | 0.2.0 |

## Version History

| Date | Package | Version | Notes |
| --- | --- | --- | --- |
| 2026-07-18 | MeshTransit.Client | 0.2.0 | Upgrade NetMQ 4.0.1.13 -> 4.0.4.2 (net10.0 target) to drop the vulnerable System.Drawing.Common 5.0.0 / System.Security.Cryptography.Xml 5.0.0 transitive chain (NU1902/NU1904). Removed the now-unneeded NU1902/1903/1904 suppression. |
| 2026-07-18 | MeshTransit | 0.2.0 | Upgrade NetMQ 4.0.1.13 -> 4.0.4.2 (first NetMQ with a net10.0 target), eliminating the fallback netstandard2.1 stack that dragged in System.ServiceModel.Primitives 4.9.0 -> ... -> System.Drawing.Common 5.0.0 (NU1902/NU1904 CVEs). Removed the now-unneeded NU1902/1903/1904 suppression. Public API unchanged (AddMeshTransitServer/AddMeshTransitHeartbeat intact). |
| 2026-07-18 | MeshTransit.Contracts | 0.2.0 | Rebuild on net10; no functional change. Chain-published with MeshTransit/MeshTransit.Client 0.2.0 (NetMQ 4.0.1.13 -> 4.0.4.2 drops the vulnerable System.Drawing.Common 5.0.0 / System.Security.Cryptography.Xml 5.0.0 transitive chain, NU1902/NU1904). Built from 87e04a4 (full Heartbeat API). |
