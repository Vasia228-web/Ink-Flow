# Локальні правки вбудованої копії MCP for Unity

Джерело: https://github.com/CoplayDev/unity-mcp, тег **v10.3.0** (теку `MCPForUnity/` скопійовано сюди; ліцензія MIT — `LICENSE-unity-mcp.txt`).
Сервер (Python) не вбудовано: Unity запускає його через `uvx --from mcpforunityserver==10.3.0`, версія збігається з пакетом.

Правки (при оновленні пакета повторити):

1. `Runtime/MCPForUnity.Runtime.asmdef` — `includePlatforms: ["Editor"]`. У вихідному пакеті модуль Runtime компілюється для всіх платформ і потрапив би в збірку iOS/Android; тепер пакет лише для редактора.
2. `Editor/Helpers/TelemetryHelper.cs` — `IsEnabled` завжди `false` (поле `LocalForceDisabled`).
3. `Editor/Services/ServerManagementService.cs` — сервер стартує зі змінними `DISABLE_TELEMETRY=1` і `UNITY_MCP_DISABLE_TELEMETRY=1`.

Група інструментів `scripting_ext` (`execute_code`) і `asset_gen` вимкнені за замовчуванням; вмикати лише з «ок» автора.
