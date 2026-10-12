import json, sys
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')

directory = Path(sys.argv[1])
report = json.loads((directory/'layout-audit.json').read_text(encoding='utf-8-sig'))
print({key:report[key] for key in ['screenshots','checkedTexts','layoutIssues','failedFlowChecks']})
seen = set()
for shot in directory.glob('*.audit.json'):
    for issue in json.loads(shot.read_text(encoding='utf-8-sig'))['issues']:
        path = '/'.join(issue['path'].split('/')[4:])
        key = (path, issue['text'],issue['kind'])
        if key in seen: continue
        seen.add(key)
        print(f"{shot.stem}: {issue['kind']} {path} | {issue['text'][:105]} | {issue['rectWidth']:.0f}x{issue['rectHeight']:.0f} needs {issue['fittedHeight']:.0f} font {issue['renderedFontSize']}")
