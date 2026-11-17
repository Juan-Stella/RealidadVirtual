using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Llama a Google.XR.Cardboard.Api.Recenter() sin dependencia de compilacion.
/// Si el paquete Cardboard no esta instalado, simplemente no hace nada.
/// </summary>
public static class CardboardRecenterProxy
{
    public static bool TryRecenter()
    {
        try
        {
            Type apiType = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                apiType = asm.GetType("Google.XR.Cardboard.Api");
                if (apiType != null) break;
            }

            if (apiType == null) return false;
            MethodInfo m = apiType.GetMethod("Recenter", BindingFlags.Public | BindingFlags.Static);
            if (m == null) return false;
            m.Invoke(null, null);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Cardboard] No se pudo recentrar: " + ex.Message);
            return false;
        }
    }
}
