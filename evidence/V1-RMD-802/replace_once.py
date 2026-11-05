import sys

path, old_path, new_path = sys.argv[1:4]
body = open(path, 'rb').read().decode('utf-8')
old = open(old_path, 'rb').read().decode('utf-8')
new = open(new_path, 'rb').read().decode('utf-8')
if '\r\n' in body:
    old = old.replace('\n', '\r\n')
    new = new.replace('\n', '\r\n')
if body.count(old) != 1:
    sys.exit('mutation target not unique: %d' % body.count(old))
open(path, 'wb').write(body.replace(old, new).encode('utf-8'))
