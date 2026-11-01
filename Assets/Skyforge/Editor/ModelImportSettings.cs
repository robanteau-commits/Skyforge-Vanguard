using UnityEditor;

namespace Skyforge.EditorTools
{
    /// <summary>
    /// Import settings for the Blender models in Assets/Skyforge/Resources/Models (docs/BLENDER.md),
    /// applied automatically so a dropped-in .fbx needs no clicking in the Inspector:
    /// 1 Blender metre = 1 game unit, Blender's axes baked so the model isn't rotated, the facet
    /// normals from Blender kept (flat shading), and no animation, cameras or lights.
    /// Materials still import (by name); the game swaps them for its own at runtime.
    /// </summary>
    public sealed class ModelImportSettings : AssetPostprocessor
    {
        const string ModelFolder = "Assets/Skyforge/Resources/Models/";

        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(ModelFolder)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }
    }
}
