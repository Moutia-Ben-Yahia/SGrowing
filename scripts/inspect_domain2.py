import os, glob

repos = ['twenty', 'espocrm', 'crm', 'laravel-crm', 'DeskcommCRM', 'Quickly', 'qrev', 'DenchClaw', 'relaticle', 'trycrm']
for r in repos:
    p = os.path.join('inspire', r)
    print('===', r, '===')
    if not os.path.exists(p):
        print('  NOT FOUND')
        continue
    items = os.listdir(p)[:8]
    print('  Root items:', items)
    # Find schema / models / entities
    matches = (glob.glob(f'{p}/**/schema*', recursive=True) + 
               glob.glob(f'{p}/**/models*', recursive=True) + 
               glob.glob(f'{p}/**/entities*', recursive=True) + 
               glob.glob(f'{p}/**/*.prisma', recursive=True))
    matches = [os.path.relpath(m, p) for m in matches if 'node_modules' not in m and '.git' not in m and 'vendor' not in m][:5]
    print('  Schema/model files:', matches)
