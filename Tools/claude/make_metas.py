"""Creates missing Unity .meta files for everything under Assets/ (random GUIDs).

Unity writes these itself when it imports a new file, but the editor runs on Rob's PC, so files
added from elsewhere (e.g. a cloud session) need their .meta committed alongside them; otherwise
each machine invents its own GUIDs. Run from the repo root: python Tools/claude/make_metas.py
"""
import os
import uuid

ROOT = 'Assets'
SKIP_EXT = {'.meta'}

SCRIPT = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
ASMDEF = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
TEXT = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
DEFAULT = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""


def template(path, is_dir):
    if is_dir:
        return FOLDER
    ext = os.path.splitext(path)[1].lower()
    if ext == '.cs':
        return SCRIPT
    if ext == '.asmdef':
        return ASMDEF
    if ext in ('.txt', '.md', '.json', '.xml', '.csv'):
        return TEXT
    return DEFAULT


def main():
    made = 0
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames if not d.startswith('.') and d != 'Temp']
        entries = [(d, True) for d in dirnames] + [(f, False) for f in filenames]
        for name, is_dir in entries:
            if name.startswith('.') or os.path.splitext(name)[1] in SKIP_EXT:
                continue
            path = os.path.join(dirpath, name)
            meta = path + '.meta'
            if os.path.exists(meta):
                continue
            with open(meta, 'w', newline='\n') as f:
                f.write(template(path, is_dir).format(guid=uuid.uuid4().hex))
            made += 1
            print('created', meta)
    print(made, 'meta file(s) created')


if __name__ == '__main__':
    main()
