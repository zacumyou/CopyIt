# Copy It 0.6.4 — game compatibility review (2026-11-12)

The installed November Game.dll build is now accepted after API review:
`F076DF4B70AA0F21A80345E03A052CA2273BC6754F41DAB7E63D837837A43745`.
The previously reviewed `AAEE15C4FA41C130ABAA1183840667E4FAAE531D67E8A9FA7536618EFFA2F86A` build remains accepted. Other builds still stop before registering tools and UI systems.

The current ObjectToolBaseSystem and BulldozeToolSystem decompilations match the previous reviewed snapshots. CreateDefinitions still has 24 parameters, including PlacementOverrides and JobHandle. The private bulldozer CreateDefinitionsJob still exposes the expected fixed fields and component/buffer lookups. Native GenerateObjectsSystem/GenerateNodesSystem remain in Modification1 and CourseSplitSystem in PostTool. Existing scope validation, native deletion, preview and settings behavior are unchanged.

Validation uses the installed game assemblies, official SDK/postprocessor, the existing placement/deletion metadata checker and focused policy tests. This is API/build validation, not gameplay validation. The user will test in game.

The official toolchain wiki returned HTTP 401 on the review date. Previously recorded official guidance and the current installed official SDK/API were used. No game binaries or decompiled game code are included in the public source.

현재 게임 빌드를 API 검토 후 허용했습니다. 이전 검토 빌드와 미검토 빌드 보호 차단은 유지합니다. 실게임 테스트는 사용자 확인 사항입니다.
