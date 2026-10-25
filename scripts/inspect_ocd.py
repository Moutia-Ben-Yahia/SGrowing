with open('inspire/Open-Cold-Dialer/backend/src/db/schema.ts', 'r', encoding='utf-8', errors='ignore') as f:
    lines = f.readlines()
for l in lines[:30]:
    print(l.rstrip())
