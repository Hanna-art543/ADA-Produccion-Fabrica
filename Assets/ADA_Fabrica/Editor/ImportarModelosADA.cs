using UnityEditor;

namespace Fabrica.UI.EditorADA
{
    /// <summary>Importa los FBX de Assets/ADA_Fabrica/Modelos sin cámaras, luces ni animaciones (más livianos).</summary>
    public class ImportarModelosADA : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(EscenaFinalADA.CarpetaModelos)) return;
            var imp = (ModelImporter)assetImporter;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importAnimation = false;
            imp.animationType = ModelImporterAnimationType.None;
            imp.isReadable = false;
        }
    }
}
