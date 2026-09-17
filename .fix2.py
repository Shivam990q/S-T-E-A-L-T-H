import io, os
p = 'S-T-E-A-L-T-H.cs'
s = io.open(p, encoding='utf-8').read()
bs = chr(92)
old = 'Get-ChildItem "$env:LOCALAPPDATA' + bs*4 + 'Microsoft' + bs*4 + 'Office" -Recurse -Include *.log,*.etl'
new = 'Get-ChildItem \\"$env:LOCALAPPDATA' + bs*4 + 'Microsoft' + bs*4 + 'Office\\" -Recurse -Include *.log,*.etl'
assert s.count(old) == 1, s.count(old)
s = s.replace(old, new)
io.open(p + '.tmp', 'w', encoding='utf-8', newline='').write(s)
os.replace(p + '.tmp', p)
print('fixed get-childitem quotes')
