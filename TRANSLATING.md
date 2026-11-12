# 사용자 번역 / User translations

Copy It과 Gizmo XYZ 0.6.0은 게임의 표준 LocalizationDictionary / cs2/l10n을 사용합니다. I18N Everywhere는 선택 사항이며, 없어도 한국어·영어가 제공됩니다.

## 번역 파일 만들기

1. 번역할 모드 폴더의 `lang/en-US.json`을 복사합니다.
2. 지원하려는 게임 언어 코드로 이름을 바꿉니다. 예: `lang/ja-JP.json`, `lang/de-DE.json`.
3. JSON의 왼쪽 키는 그대로 두고 오른쪽 문구만 번역합니다. UTF-8로 저장합니다. `{0}`, `{1}` 등의 자리표시자는 보존하되 문장 안의 순서는 바꿀 수 있습니다.
4. I18N Everywhere를 활성화하고 게임에서 해당 언어를 선택합니다. 파일 변경 후 I18N의 Reload를 사용합니다. 이미 표시된 UI가 갱신되지 않으면 언어를 전환하거나 게임을 다시 실행합니다.
5. 기존 한국어/영어 문구를 덮어쓰려면 I18N 설정의 **Enable overwrite / 덮어쓰기**가 필요합니다.

이 모드의 `lang`에는 도구 UI·툴팁·상태 메시지가 들어 있습니다. 설정 메뉴와 단축키 이름까지 포함한 전체 사전은 I18N 개발자 메뉴에서 해당 모드의 `Locale` 소스를 선택해 내보낼 수 있습니다. 게임의 `Options.*` 키도 동일한 JSON에 추가할 수 있습니다.

번역 키의 뒤쪽 식별자는 변경하지 마세요. 번역이 빠진 항목은 영어 기본값으로 표시됩니다. 에셋 이름·숫자·키 이름은 실행 중 인수로 들어갑니다. 메시지 전체를 번역하므로 필요한 언어별 어순으로 작성할 수 있습니다. 파일 내용은 코드로 실행되지 않습니다.

## 업데이트와 별도 번역팩

기본 `en-US.json`/`ko-KR.json`은 모드 업데이트 시 갱신됩니다. 직접 수정한 파일은 별도 보관하세요. 배포하거나 업데이트와 분리해서 관리하려면 I18N의 별도 언어팩 기능을 이용하는 편이 좋습니다.

확인한 I18N 구현은 `ModsData/<이름>Localization/i18n.json`이 있는 언어팩을 지원합니다(새 모드 탐색 및 언어팩 로딩 옵션 필요). 번역 파일은 그 폴더 아래 `Localization/<언어코드>/CopyIt.json` 또는 `GizmoXYZ.json`에 둡니다. `i18n.json` 예:

```json
{
  "Name": "My mod translations",
  "IncludedLanguage": "ja-JP",
  "Author": "Your name",
  "Description": "Translations for Copy It and Gizmo XYZ"
}
```

언어팩 탐색 방식은 설치된 I18N 버전에 따라 달라질 수 있습니다. 이 작업에서는 I18N 자체를 설치하거나 구독 설정을 바꾸지 않았습니다.

## English

Copy `lang/en-US.json` to `lang/<game-locale-id>.json` and translate the values only. Keep keys and numbered placeholders unchanged; placeholders may be reordered. Both mods register their catalogs through the game's native localization source and consume `cs2/l10n`, with no I18N binary dependency. Missing translations use English. I18N's overwrite option is required to replace existing native English/Korean entries. Export each mod's `Locale` source from I18N to include options/keybinding strings. Use a separate language pack to keep custom translations independent of mod updates.

For contributors: edit `localization/catalog.json`, then run `node tools/generate-localization.cjs`. UI builds regenerate native catalogs and `lang/*.json`; rebuild C# after changing catalog entries. Never rename published keys merely to reword a translation.

## 확인한 소스 / Reviewed source

Reviewed 2026-09-16, I18NEveryWhere commit `d9285c2490079c6d67303da536207b47b106ce64`:
- https://github.com/baka-gourd/I18NEveryWhere/blob/d9285c2490079c6d67303da536207b47b106ce64/I18NEverywhere/Mod.cs — per-mod `lang/<locale>.json`, language-pack discovery and priorities.
- https://github.com/baka-gourd/I18NEveryWhere/blob/d9285c2490079c6d67303da536207b47b106ce64/I18NEverywhere/HookLocalizationDictionary.cs — standard dictionary lookup and overwrite option.
- https://github.com/baka-gourd/I18NEveryWhere/blob/d9285c2490079c6d67303da536207b47b106ce64/I18NEverywhere/Setting.cs — native dictionary source export and reload.

Build/render tests verify catalogs, placeholders and override lookup. Actual I18N loading inside Cities: Skylines II still needs runtime validation.
