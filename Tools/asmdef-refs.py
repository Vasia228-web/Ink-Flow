#!/usr/bin/env python3
"""Друкує InkFlow-збірки, які asmdef оголошує ПРЯМО.

Саме прямо, без транзитивного розкриття: перевірено по rsp самого Unity —
він передає компілятору рівно оголошені посилання. Якщо збірка користується
типом «через сусіда», Unity падає з CS0234, і перевірка мусить падати так само.
"""
import json
import os
import sys


def load_asmdefs(root):
    """assembly name -> path, для всіх asmdef у Assets."""
    found = {}
    for base, _dirs, files in os.walk(os.path.join(root, "Assets")):
        for f in files:
            if not f.endswith(".asmdef"):
                continue
            path = os.path.join(base, f)
            try:
                with open(path, encoding="utf-8") as fh:
                    data = json.load(fh)
            except Exception:
                continue
            name = data.get("name")
            if name:
                found[name] = data
    return found


def direct(name, table):
    data = table.get(name)
    if not data:
        return set()
    # GUID-посилання (guid:...) не використовуємо — у проєкті лише імена.
    return {ref for ref in data.get("references", []) if ref.startswith("InkFlow.")}


def main():
    if len(sys.argv) != 3:
        print("usage: asmdef-refs.py <project-root> <assembly-name>", file=sys.stderr)
        return 2

    root, assembly = sys.argv[1], sys.argv[2]
    table = load_asmdefs(root)
    for ref in sorted(direct(assembly, table)):
        print(ref)
    return 0


if __name__ == "__main__":
    sys.exit(main())
