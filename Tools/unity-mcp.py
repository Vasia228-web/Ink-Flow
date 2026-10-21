#!/usr/bin/env python3
"""Клієнт MCP for Unity з термінала — для сесій і помічників, у яких немає інструментів MCP.

Сервер живе в редакторі (Window → MCP for Unity → Start Server, http://127.0.0.1:8080/mcp).
Нічого, крім localhost, скрипт не чіпає; execute_code не викликає (рішення автора).

  python3 Tools/unity-mcp.py status                  — чи є зв'язок і чи редактор компілює
  python3 Tools/unity-mcp.py console [N] [--errors]  — останні N записів консолі (помилки/попередження/логи)
  python3 Tools/unity-mcp.py clear                   — очистити консоль
  python3 Tools/unity-mcp.py menu "Ink Flow/Setup/Build Hub Screen"
  python3 Tools/unity-mcp.py refresh                 — AssetDatabase.Refresh + дочекатися компіляції
  python3 Tools/unity-mcp.py tests [EditMode] [фільтр] — запустити тести й дочекатися підсумку
  python3 Tools/unity-mcp.py test-job <id>           — дочекатися вже запущеної задачі тестів
  python3 Tools/unity-mcp.py call <інструмент> '<json-аргументи>'

Код виходу: 0 — успіх; 1 — інструмент повернув помилку чи тести впали; 2 — немає зв'язку.
"""
import json
import sys
import time
import urllib.error
import urllib.request

URL = 'http://127.0.0.1:8080/mcp'
_session = None
_next_id = 1


def _post(body, notify=False, timeout=600):
    global _session
    headers = {'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream'}
    if _session:
        headers['Mcp-Session-Id'] = _session
    request = urllib.request.Request(URL, data=json.dumps(body).encode(), headers=headers, method='POST')
    response = urllib.request.urlopen(request, timeout=timeout)
    if not _session:
        _session = response.headers.get('Mcp-Session-Id')
    text = response.read().decode()
    if notify or not text.strip():
        return None
    for line in text.splitlines():
        if line.startswith('data:'):
            return json.loads(line[5:])
    return json.loads(text)


def connect():
    try:
        _post({'jsonrpc': '2.0', 'id': 0, 'method': 'initialize',
               'params': {'protocolVersion': '2025-03-26', 'capabilities': {},
                          'clientInfo': {'name': 'inkflow-cli', 'version': '1'}}}, timeout=10)
        _post({'jsonrpc': '2.0', 'method': 'notifications/initialized'}, notify=True, timeout=10)
    except (urllib.error.URLError, ConnectionError, TimeoutError) as e:
        print(f'Немає зв\'язку з MCP for Unity ({e}). Відкрий Unity → Window → MCP for Unity → Start Server.')
        sys.exit(2)


def call(name, args, timeout=600):
    global _next_id
    _next_id += 1
    reply = _post({'jsonrpc': '2.0', 'id': _next_id, 'method': 'tools/call',
                   'params': {'name': name, 'arguments': args}}, timeout=timeout)
    result = (reply or {}).get('result') or {}
    data = result.get('structuredContent')
    if data is None:
        content = result.get('content') or [{}]
        try:
            data = json.loads(content[0].get('text', '{}'))
        except json.JSONDecodeError:
            data = {'text': content[0].get('text')}
    # Частина інструментів (run_tests, get_test_job) загортає відповідь у {"result": {...}}.
    if isinstance(data, dict) and set(data.keys()) == {'result'} and isinstance(data['result'], dict):
        data = data['result']
    if result.get('isError') or (reply or {}).get('error'):
        data = data or {}
        data.setdefault('success', False)
        data.setdefault('error', (reply or {}).get('error'))
    return data


def _strip(line):
    import re
    return re.sub(r'<[^>]+>', '', line)


def console(count=30, errors_only=False):
    types = ['error'] if errors_only else ['error', 'warning', 'log']
    data = call('read_console', {'action': 'get', 'types': types, 'count': 500, 'format': 'plain'})
    lines = (data or {}).get('data') or []
    for line in lines[-count:]:
        print('-', _strip(str(line))[:600])
    return lines


def editor_state():
    """Ресурс mcpforunity://editor/state: компіляція, імпорт, Play Mode, тести."""
    global _next_id
    _next_id += 1
    reply = _post({'jsonrpc': '2.0', 'id': _next_id, 'method': 'resources/read',
                   'params': {'uri': 'mcpforunity://editor/state'}}, timeout=30) or {}
    contents = (reply.get('result') or {}).get('contents') or [{}]
    try:
        return (json.loads(contents[0].get('text', '{}')).get('data')) or {}
    except json.JSONDecodeError:
        return {}


def busy(state):
    return bool((state.get('compilation') or {}).get('is_compiling')
                or (state.get('compilation') or {}).get('is_domain_reload_pending')
                or (state.get('assets') or {}).get('is_updating')
                or ((state.get('assets') or {}).get('refresh') or {}).get('is_refresh_in_progress'))


def wait_idle(limit=600):
    """Чекає, поки редактор не компілює й не імпортує. Під час перезавантаження домену сервер
    тимчасово не відповідає — це теж «зайнятий»."""
    start = time.time()
    time.sleep(1)
    while time.time() - start < limit:
        try:
            if not busy(editor_state()):
                return True
        except (urllib.error.URLError, ConnectionError, TimeoutError, json.JSONDecodeError):
            pass
        time.sleep(2)
    return False


def tests(mode='EditMode', test_filter=None, job=None):
    args = {'mode': mode}
    if test_filter:
        args['test_names'] = [test_filter] if '.' in test_filter else None
        args['assembly_names'] = None if '.' in test_filter else [test_filter]
        args = {k: v for k, v in args.items() if v is not None}
    if job is None:
        started = call('run_tests', args)
        job = ((started or {}).get('data') or {}).get('job_id')
    else:
        started = {}
    if not job:
        print('Тести не стартували:', json.dumps(started, ensure_ascii=False)[:800])
        return 1
    while True:
        state = call('get_test_job', {'job_id': job, 'include_failed_tests': True, 'wait_timeout': 30}) or {}
        d = state.get('data') or {}
        if d.get('status') != 'running':
            summary = ((d.get('result') or {}).get('summary')) or {}
            print(f"Тести {mode}: {summary.get('passed')}/{summary.get('total')} пройшли, "
                  f"впало {summary.get('failed')}, пропущено {summary.get('skipped')} ({summary.get('resultState')})")
            for failure in ((d.get('progress') or {}).get('failures_so_far') or [])[:30]:
                print('  ✗', json.dumps(failure, ensure_ascii=False)[:500])
            for r in ((d.get('result') or {}).get('results') or [])[:30]:
                print('  ✗', json.dumps(r, ensure_ascii=False)[:500])
            return 0 if summary.get('failed') == 0 and summary.get('total') else 1
        time.sleep(3)


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 1
    connect()
    cmd = argv[1]
    if cmd == 'status':
        st = editor_state()
        ed = st.get('editor') or {}
        print(f"зв'язок є; сцена {(ed.get('active_scene') or {}).get('path')}; "
              f"Play Mode: {(ed.get('play_mode') or {}).get('is_playing')}; зайнятий: {busy(st)}; "
              f"тести йдуть: {(st.get('tests') or {}).get('is_running')}")
        return 0
    if cmd == 'console':
        count = int(argv[2]) if len(argv) > 2 and argv[2].isdigit() else 30
        console(count, '--errors' in argv)
        return 0
    if cmd == 'clear':
        print(json.dumps(call('read_console', {'action': 'clear'}), ensure_ascii=False)[:300])
        return 0
    if cmd == 'menu':
        before = len(call('read_console', {'action': 'get', 'types': ['error', 'warning', 'log'], 'count': 500, 'format': 'plain'}).get('data') or [])
        data = call('execute_menu_item', {'menu_path': argv[2]})
        ok = (data or {}).get('success', False)
        time.sleep(1)
        wait_idle()
        lines = call('read_console', {'action': 'get', 'types': ['error', 'warning', 'log'], 'count': 500, 'format': 'plain'}).get('data') or []
        for line in lines[before:]:
            print('-', _strip(str(line))[:600])
        return 0 if ok else 1
    if cmd == 'refresh':
        print(json.dumps(call('refresh_unity', {'mode': 'force', 'scope': 'all', 'compile': 'request', 'wait_for_ready': True}), ensure_ascii=False)[:600])
        wait_idle()
        return 0
    if cmd == 'tests':
        mode = argv[2] if len(argv) > 2 else 'EditMode'
        return tests(mode, argv[3] if len(argv) > 3 else None)
    if cmd == 'test-job':
        return tests(job=argv[2])
    if cmd == 'call':
        if argv[2] == 'execute_code':
            print('execute_code заборонено без «ок» автора.')
            return 1
        print(json.dumps(call(argv[2], json.loads(argv[3]) if len(argv) > 3 else {}), ensure_ascii=False, indent=1)[:6000])
        return 0
    print(__doc__)
    return 1


if __name__ == '__main__':
    sys.exit(main(sys.argv))
