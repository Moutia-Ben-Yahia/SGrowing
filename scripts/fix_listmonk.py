import sys
sys.path.append('.')
from scripts.update_domain5_6 import listmonk_seq

with open('docs/wiki/listmonk.md', 'r', encoding='utf-8') as f:
    content = f.read()

if 'sequenceDiagram' not in content:
    idx = content.find('## 8. Configuration & Deployment')
    if idx != -1:
        content = content[:idx] + "\n" + listmonk_seq.strip() + "\n\n---\n\n" + content[idx:]
    else:
        idx2 = content.find('## 9. Strategic Value')
        content = content[:idx2] + "\n" + listmonk_seq.strip() + "\n\n---\n\n" + content[idx2:]

with open('docs/wiki/listmonk.md', 'w', encoding='utf-8') as f:
    f.write(content)

print('Updated listmonk.md with sequence diagram!')
