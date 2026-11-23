#if UNITY_ANDROID
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Android;

public static class LogcatAndroid
{
    [MenuItem("FrankaVR/Guardar log del celular")]
    static void Guardar()
    {
        string adb = Path.Combine(AndroidExternalToolsSettings.sdkRootPath, "platform-tools", "adb.exe");
        string salida = Path.GetFullPath("Verificacion/logcat_android.txt");
        string pkg = PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
        Correr(adb, "logcat -c");
        Correr(adb, "shell am force-stop " + pkg);
        Correr(adb, "shell monkey -p " + pkg + " -c android.intent.category.LAUNCHER 1");
        System.Threading.Thread.Sleep(25000);
        string texto = Correr(adb, "devices") + "\n" + Correr(adb, "logcat -d -v time");
        File.WriteAllText(salida, texto);
        UnityEngine.Debug.Log("Log del celular guardado en " + salida);
    }

    static string Correr(string exe, string args)
    {
        var p = Process.Start(new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        });
        string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return o;
    }
}
#endif
