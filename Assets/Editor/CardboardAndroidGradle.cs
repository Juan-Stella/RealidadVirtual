#if UNITY_ANDROID
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Completa el proyecto Gradle de Android con lo que pide la guia oficial de Cardboard
/// (developers.google.com/cardboard/develop/unity/quickstart, "Publishing Settings"),
/// sin plantillas personalizadas: se edita el proyecto recien generado en cada build.
/// - Dependencias Java del SDK de Cardboard: sin ellas el escaneo del QR del visor
///   (Api.ScanDeviceParams, primer arranque) cierra la app.
/// - AndroidX + Jetifier en gradle.properties.
/// - Permiso ACCESS_NETWORK_STATE: el receptor UDP del mando lo usa para atar la app a la
///   red Wi-Fi del ESP32 (que no tiene internet) aunque los datos moviles esten prendidos.
/// Todo es idempotente: si la linea ya esta, no se repite.
/// </summary>
public class CardboardAndroidGradle : IPostGenerateGradleAndroidProject
{
    static readonly string[] Dependencies =
    {
        "androidx.appcompat:appcompat:1.6.1",
        "com.google.android.gms:play-services-vision:20.1.3",
        "com.google.android.material:material:1.12.0",
        "com.google.protobuf:protobuf-javalite:3.19.4",
    };

    public int callbackOrder => 100;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // path = carpeta unityLibrary del proyecto Gradle exportado.
        AddDependencies(Path.Combine(path, "build.gradle"));
        AddGradleProperties(Path.Combine(Directory.GetParent(path).FullName, "gradle.properties"));
        AddPermission(Path.Combine(path, "src", "main", "AndroidManifest.xml"), "android.permission.ACCESS_NETWORK_STATE");
    }

    static void AddDependencies(string file)
    {
        if (!File.Exists(file)) { Debug.LogWarning("[Cardboard Gradle] No existe " + file); return; }
        string text = File.ReadAllText(file);
        string block = "";
        foreach (string dep in Dependencies)
        {
            string name = dep.Substring(0, dep.LastIndexOf(':'));
            if (!text.Contains(name)) block += "    implementation '" + dep + "'\n";
        }
        if (block.Length == 0) return;
        File.AppendAllText(file, "\n// Cardboard SDK (agregado por CardboardAndroidGradle)\ndependencies {\n" + block + "}\n");
        Debug.Log("[Cardboard Gradle] Dependencias agregadas a " + file);
    }

    static void AddGradleProperties(string file)
    {
        string text = File.Exists(file) ? File.ReadAllText(file) : "";
        string extra = "";
        if (!Regex.IsMatch(text, @"^\s*android\.useAndroidX\s*=", RegexOptions.Multiline)) extra += "android.useAndroidX=true\n";
        if (!Regex.IsMatch(text, @"^\s*android\.enableJetifier\s*=", RegexOptions.Multiline)) extra += "android.enableJetifier=true\n";
        if (extra.Length == 0) return;
        if (text.Length > 0 && !text.EndsWith("\n")) extra = "\n" + extra;
        File.AppendAllText(file, extra);
    }

    static void AddPermission(string file, string permission)
    {
        if (!File.Exists(file)) { Debug.LogWarning("[Cardboard Gradle] No existe " + file); return; }
        string text = File.ReadAllText(file);
        if (text.Contains(permission)) return;
        int manifest = text.IndexOf("<manifest", System.StringComparison.Ordinal);
        int close = manifest >= 0 ? text.IndexOf('>', manifest) : -1;
        if (close < 0) { Debug.LogWarning("[Cardboard Gradle] Manifest sin <manifest>: " + file); return; }
        text = text.Insert(close + 1, "\n    <uses-permission android:name=\"" + permission + "\" />");
        File.WriteAllText(file, text);
    }
}
#endif
