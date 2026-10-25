import os

repos = ['twenty', 'espocrm', 'crm', 'laravel-crm', 'DeskcommCRM', 'Quickly', 'qrev', 'DenchClaw', 'relaticle', 'trycrm']
for r in repos:
    p = os.path.join('inspire', r)
    exists = os.path.exists(p)
    wiki_p = os.path.join('docs/wiki', r + '.md')
    wiki_lines = 0
    if os.path.exists(wiki_p):
        with open(wiki_p, 'r', encoding='utf-8') as f:
            wiki_lines = len(f.readlines())
    print(f'{r:15} exists={exists} wiki_lines={wiki_lines}')
