import os
import re

def inspect_ocd():
    p = 'inspire/Open-Cold-Dialer/backend/src/db/schema.ts'
    if os.path.exists(p):
        with open(p, 'r', encoding='utf-8', errors='ignore') as f:
            text = f.read()
        tables = re.findall(r'export const (\w+) = pgTable\([\'"](\w+)[\'"]', text)
        print('Open-Cold-Dialer tables:', tables)

def inspect_ringee():
    p = 'inspire/ringee-app/packages/database/prisma/schema.prisma'
    if os.path.exists(p):
        with open(p, 'r', encoding='utf-8', errors='ignore') as f:
            text = f.read()
        models = re.findall(r'model\s+(\w+)\s+{', text)
        print('ringee-app models:', models[:15])

def inspect_voicera():
    p = 'inspire/VoicEra/apps/api/app/models/schemas.py'
    if os.path.exists(p):
        with open(p, 'r', encoding='utf-8', errors='ignore') as f:
            text = f.read()
        classes = re.findall(r'class\s+(\w+)', text)
        print('VoicEra classes:', classes[:15])

def inspect_dograh():
    p = 'inspire/dograh/api/db/models.py'
    if os.path.exists(p):
        with open(p, 'r', encoding='utf-8', errors='ignore') as f:
            text = f.read()
        classes = re.findall(r'class\s+(\w+)', text)
        print('dograh classes:', classes[:15])

inspect_ocd()
inspect_ringee()
inspect_voicera()
inspect_dograh()
