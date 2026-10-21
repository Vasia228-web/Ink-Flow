---
name: unity-rebuild
description: Перезбирання екранів SpacePaint у відкритому Unity через MCP — пункти меню Build … Screen, Build Main Scene, Refresh Picture Library, знімки, з читанням консолі. Вмикай після правки збирача чи компонента UI, на фрази «перезбери екран», «Build Hub Screen», «зроби знімки», «запусти в редакторі». Якщо редактор закритий — скажи прямо й переходь на batch на копії.
model: sonnet
---

# Перезбирання в Unity

Сцени й префаби екранів руками не правимо: міняємо збирач і запускаємо його пунктом меню.

1. `python3 Tools/unity-mcp.py status` — зв'язок, не Play Mode, не компілює. Немає зв'язку → редактор закритий: пиши «в редакторі не перевірено» й роби batch на копії (`docs/gotchas.md`, «Batch-режим при відкритому редакторі»).
2. Після правки C#: `python3 Tools/unity-mcp.py refresh` (імпорт і компіляція), потім `python3 Tools/unity-mcp.py console 40 --errors` — помилок компіляції бути не має.
3. Порядок меню (кожен — `python3 Tools/unity-mcp.py menu "Ink Flow/Setup/…"`): `Build UI Kit` (лише якщо міняв спрайти, шрифти, атоми, токени) → `Refresh Picture Library` (якщо міняв картинки) → потрібні `Build … Screen` → **`Build Main Scene` завжди останнім**.
4. Знімки: `Ink Flow/Debug/Capture Meta Screenshots` (мета-екрани) або `Ink Flow/Debug/Capture Run Screenshots` (забіг). У консолі — рядки UiAudit і бюджету викликів; «вада» = червоне.
5. Наприкінці — `console 60 --errors`: нових помилок немає. `git status` — змінилися лише очікувані сцени/префаби.

`execute_code` (довільний код у редакторі) не вмикати без «ок» автора.
