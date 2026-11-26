using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>
/// El URDF Importer trae las librerias nativas de Assimp para Windows, Linux y macOS marcadas
/// para "cualquier plataforma". En Android no sirven (las mallas del robot ya estan importadas)
/// y desde Unity 6.5 el build falla porque las dos assimp.dll de Windows tienen el mismo nombre.
/// No se puede editar el .meta del paquete (es de solo lectura): se las excluye del build Android.
/// </summary>
[InitializeOnLoad]
public class ExcluirAssimpDeAndroid : IPreprocessBuildWithReport
{
    static ExcluirAssimpDeAndroid() { Apply(); }

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report) { Apply(); }

    static void Apply()
    {
        foreach (PluginImporter plugin in PluginImporter.GetAllImporters())
        {
            if (plugin.assetPath.Replace('\\', '/').Contains("/AssimpNet/Native/"))
                plugin.SetIncludeInBuildDelegate(_ => EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android);
        }
    }
}
