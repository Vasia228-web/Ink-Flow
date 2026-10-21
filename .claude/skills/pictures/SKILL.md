---
name: pictures
description: Додати чи змінити піксельні картинки SpacePaint — опис фігурами в Tools/pictures/author.py, перевірка правил, контактний аркуш, Refresh Picture Library у редакторі. Вмикай на фрази «нова картинка», «додай картинку», «перемалюй», «назва картинки», «аркуш картинок».
model: sonnet
---

# Картинки

Картинка — це фігури в `Tools/pictures/author.py`, не сітка руками. Правила формату — `docs/pictures-format.md` (розмір ~32 px, 10–16 тонів, 4–6 родин, ≥ 3 затінені, кроки в межах цілі ±35 %); їх тримає `PictureLibraryTests`.

1. Опиши/зміни картинку в `author.py` (id латиницею — назва у файлі збереження, ніколи не перейменовувати; назва для гравця — українською, з великої літери, довгі слова з м'яким переносом U+00AD).
2. `python3 Tools/pictures/author.py --check` — правила; потім `python3 Tools/pictures/author.py` — запис `Assets/_Pictures/**/*.txt`.
3. `python3 Tools/pictures/contact_sheet.py` — аркуш `docs/pictures-contact-sheet.png`; подивись його очима.
4. У редакторі: `python3 Tools/unity-mcp.py menu "Ink Flow/Setup/Refresh Picture Library"`, далі за потреби скіл `unity-rebuild`.
5. `bash Tools/run-core-tests.sh` — `PictureLibraryTests` зелені.
