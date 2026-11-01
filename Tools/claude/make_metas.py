"""Creates missing Unity .meta files for folders, scripts, asmdefs and text files under Assets/.

Unity writes these itself when it imports a new file, but code added from a cloud session (no
Unity) needs its .meta committed alongside it; otherwise each machine invents its own GUIDs.
Run from the repo root: python Tools/claude/make_metas.py

Models, textures, audio and other imported assets are SKIPPED: their .meta holds importer
settings this script can't write correctly. Let Unity import them (open the Editor), then commit
the .meta it creates.
"""
import os
import uuid

ROOT = 'Assets'
SKIP_EXT = {'.meta'}
# Only these get a .meta from this script; everything else waits for Unity.
HANDLED_EXT = {'.cs', '.asmdef', '.txt', '.md', '.json', '.xml', '.csv'}

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
            ext = os.path.splitext(name)[1].lower()
            if name.startswith('.') or ext in SKIP_EXT:
                continue
            if not is_dir and ext not in HANDLED_EXT:
                if not os.path.exists(os.path.join(dirpath, name + '.meta')):
                    print('skipped (let Unity create its .meta):', os.path.join(dirpath, name))
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
