# Copy It / 복사해!

Cities: Skylines II code mod for copying selected objects, networks and surfaces.

[한국어 모드 소개](모드%20소개글.ko.md) · [검증 및 테스트](TESTING.ko.md) · [개발 이력](README.ko.md)

## Requirements / 요구 사항

- Cities: Skylines II 1.6.*; the current binary checks the exact reviewed Game.dll hash and stops on an unreviewed build.
- Anarchy, Paradox Mods ID **74604**, is the declared publishing dependency.
- Official Cities: Skylines II modding toolchain for local development, .NET SDK with net9.0 test support, Node.js 18+.

## Build / 빌드

Install the official modding toolchain and verify its CSII_* environment variables. From this directory, run `./build.ps1` in PowerShell. The game assemblies remain external references and are not redistributed. Outputs are written to `local/CopyIt`; test logs go to `research` (both ignored by Git).

공식 모딩 툴체인을 설치하고 CSII_* 환경변수를 확인한 뒤 PowerShell에서 `./build.ps1`을 실행하세요. 게임 DLL은 저장소에 포함하지 않습니다.

## Source map / 소스 안내

- `src/CopyTool.cs`: selection → clipboard → native preview → validated placement / 선택부터 검증된 배치까지
- `src/CopySelection.cs`, `AssetSamples.cs`: spatial selection and multi-asset filters / 공간 선택과 다중 에셋 필터
- `src/CopyNetworks.cs`, `CopyNetworkStageSystem.cs`: network geometry and topology / 도로 곡선·연결 관계
- `src/CopySurfaces.cs`: native surface node definitions / 표면 꼭짓점 정의
- `src/CopyDecalPlacement.cs`, `SlopeMath.cs`: terrain fitting / 데칼 지형 정렬
- `src/CopyVisibilitySystem.cs`: persisted visibility protection / 저장되는 표시 보호
- `ui/src`: native game UI bindings, styles and SVG icons / 게임 UI·스타일·아이콘
- `tests`, `slope-tests`: logic/UI contracts and slope regression tests / 논리·UI·경사 검사

Source comments use **KO** and **EN** for maintainers of both languages.

## Save files and limitations / 저장 파일 및 한계

Placed copies are native game entities. Uninstalling Copy It does not automatically delete or undo them. Copy It also serializes a visibility-protection marker; uninstalling stops its protection systems. No zero-impact or corruption-free guarantee is made. Keep a separate save backup before using or removing the mod. Network copying is experimental.

복제물은 게임 엔티티이며 제거 시 자동 삭제되지 않습니다. 표시 보호용 태그는 저장되며 모드 제거 시 보호가 중지됩니다. 무영향이나 무결성을 보장하지 않으므로 별도 저장 백업을 권장합니다.

Automated build/tests do **not** establish in-game placement, save reload, uninstallation, or compatibility with every 1.6.* patch. See the explicit untested cases in TESTING.ko.md.

## Credits

See [NOTICE.md](NOTICE.md) for Anarchy's MIT notice and other source references. Game code, decompiled research files, personal logs and credentials are intentionally not published.