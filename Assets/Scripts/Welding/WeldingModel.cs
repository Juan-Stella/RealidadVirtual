using UnityEngine;

namespace FrankaVR.Welding
{
    /// Resultado de calidad en un punto del cordon.
    public enum WeldQuality
    {
        None,        // sin material depositado
        Cold,        // falta de fusion: poca energia
        Good,        // cordon correcto
        Undercut,    // socavado: angulo de trabajo o avance muy fuera de norma
        Porosity,    // porosidad: arco demasiado largo (perdida de proteccion gaseosa)
        BurnThrough  // perforacion: exceso de energia
    }

    /// Consumible del proceso. Hoy: alambre macizo MIG/MAG (AWS A5.18 ER70S-6) con su gas.
    /// LengthMm es el stick-out (alambre libre fuera del tubo de contacto) y no se consume:
    /// el alimentador lo repone. Con BurnRateMmPerSec > 0 vuelve a comportarse como varilla SMAW.
    [System.Serializable]
    public class Electrode
    {
        public string Name       = "E6013 3.25";
        public string Family     = "E6013";
        public float  DiameterMm = 3.25f;
        public float  LengthMm   = 350f;   // largo inicial de la varilla
        public float  AmpMin     = 90f;    // rango recomendado de corriente
        public float  AmpMax     = 130f;
        public float  ArcVoltage = 23f;    // tension de arco nominal
        public float  DepositFactor = 1f;  // eficiencia relativa de deposito
        public float  BurnRateMmPerSec = 3.2f; // consumo de varilla a corriente nominal
        [Tooltip("Tolerancia del electrodo a arco largo. E6011 tolera mas; E7018 es muy sensible.")]
        public float  ArcLengthTolerance = 1.0f;
        [Tooltip("Descripcion breve para el panel didactico.")]
        public string Notes = "Uso general, arco suave, facil de cebar.";

        [Tooltip("Largo de arco ideal (mm). 0: regla de SMAW, igual al diametro.")]
        public float  ArcIdealMm = 0f;
        public bool   WireFeed => BurnRateMmPerSec <= 0f;

        public float ArcLenIdealMm => ArcIdealMm > 0f ? ArcIdealMm : DiameterMm;
        public float AmpNominal    => (AmpMin + AmpMax) * 0.5f;
    }

    public static class ElectrodeLibrary
    {
        // MIG/MAG sinergico: una sola perilla (corriente); la tension sigue al alambre.
        // Distancia boquilla-pieza (CTWD) = stick-out + arco: 12-18 mm segun diametro.
        public static Electrode[] Defaults => new[]
        {
            new Electrode{ Name="ER70S-6 0.8 · CO2", Family="ER70S-6", DiameterMm=0.8f, LengthMm=10f, AmpMin=60f, AmpMax=150f,
                           ArcVoltage=19f, DepositFactor=0.90f, BurnRateMmPerSec=0f, ArcIdealMm=3f, ArcLengthTolerance=1.0f,
                           Notes="Chapa fina, arco corto. CO2 puro: mas penetracion y mas salpicadura." },
            new Electrode{ Name="ER70S-6 1.0 · Ar/CO2", Family="ER70S-6", DiameterMm=1.0f, LengthMm=12f, AmpMin=90f, AmpMax=220f,
                           ArcVoltage=22f, DepositFactor=1.00f, BurnRateMmPerSec=0f, ArcIdealMm=3f, ArcLengthTolerance=1.0f,
                           Notes="Uso general en celdas robotizadas. Mezcla Ar 82 / CO2 18." },
            new Electrode{ Name="ER70S-6 1.2 · Ar/CO2", Family="ER70S-6", DiameterMm=1.2f, LengthMm=15f, AmpMin=140f, AmpMax=300f,
                           ArcVoltage=26f, DepositFactor=1.12f, BurnRateMmPerSec=0f, ArcIdealMm=3.5f, ArcLengthTolerance=0.9f,
                           Notes="Espesores mayores, mucho aporte: exige mas velocidad de avance." },
        };

        /// Catalogo SMAW anterior, conservado como referencia.
        public static Electrode[] Smaw => new[]
        {
            new Electrode{ Name="E6013 2.5",  Family="E6013", DiameterMm=2.5f,  LengthMm=350f, AmpMin=60f,  AmpMax=100f, ArcVoltage=22f, DepositFactor=0.90f, BurnRateMmPerSec=3.6f, ArcLengthTolerance=1.2f,
                           Notes="Chapa fina. Arco suave y estable, poca penetracion." },
            new Electrode{ Name="E6013 3.25", Family="E6013", DiameterMm=3.25f, LengthMm=350f, AmpMin=90f,  AmpMax=130f, ArcVoltage=23f, DepositFactor=1.00f, BurnRateMmPerSec=3.2f, ArcLengthTolerance=1.2f,
                           Notes="Uso general. El mas tolerante para aprender." },
            new Electrode{ Name="E6011 3.25", Family="E6011", DiameterMm=3.25f, LengthMm=350f, AmpMin=70f,  AmpMax=120f, ArcVoltage=24f, DepositFactor=1.10f, BurnRateMmPerSec=3.4f, ArcLengthTolerance=1.4f,
                           Notes="Alta penetracion, arco violento. Tolera arco largo y chapa sucia." },
            new Electrode{ Name="E7018 3.25", Family="E7018", DiameterMm=3.25f, LengthMm=350f, AmpMin=100f, AmpMax=140f, ArcVoltage=23f, DepositFactor=1.05f, BurnRateMmPerSec=3.0f, ArcLengthTolerance=0.75f,
                           Notes="Bajo hidrogeno, estructural. Exige arco corto y constante." },
            new Electrode{ Name="E7018 4.0",  Family="E7018", DiameterMm=4.0f,  LengthMm=350f, AmpMin=130f, AmpMax=180f, ArcVoltage=24f, DepositFactor=1.15f, BurnRateMmPerSec=2.8f, ArcLengthTolerance=0.75f,
                           Notes="Espesores gruesos. Mucho aporte: perfora chapa fina en segundos." },
        };
    }

    /// Fisica del proceso SMAW simplificada pero coherente con la practica real.
    ///
    /// Aporte termico instantaneo en el punto de trabajo:
    ///     P = eta * U * I * k_deposito * f_arco * f_corriente
    /// integrado en el tiempo sobre cada punto de la costura. La velocidad de avance
    /// no es un parametro: emerge de cuanto tiempo el operador deja el arco sobre cada punto.
    public static class WeldingHeatModel
    {
        public const float ArcEfficiency = 0.8f; // eta tipico de SMAW

        /// Factor 0..1 por longitud de arco: campana centrada en el ideal (~diametro).
        /// Arco corto -> se pega. Arco largo -> pierde proteccion, salpica y porosa.
        public static float ArcLengthFactor(float arcLenMm, Electrode e)
        {
            if (arcLenMm <= 0.2f) return 0f; // electrodo pegado: no hay arco util
            float r = arcLenMm / Mathf.Max(0.1f, e.ArcLenIdealMm);
            float x = (r - 1f) / Mathf.Max(0.2f, e.ArcLengthTolerance);
            return Mathf.Clamp01(1f - 0.9f * x * x);
        }

        /// Factor por corriente respecto al rango del electrodo.
        /// Por debajo del minimo: no funde bien. Por encima del maximo: sobrecalienta (>1).
        public static float AmperageFactor(float amps, Electrode e)
        {
            if (amps < e.AmpMin) return Mathf.Clamp01(amps / Mathf.Max(1f, e.AmpMin)) * 0.6f;
            if (amps > e.AmpMax) return 1f + (amps - e.AmpMax) / e.AmpMax;
            return 1f;
        }

        /// Potencia efectiva (J/s) entregada al charco de fusion.
        public static float PowerWatts(float amps, float arcLenMm, Electrode e)
        {
            float p = ArcEfficiency * e.ArcVoltage * amps * e.DepositFactor;
            return p * ArcLengthFactor(arcLenMm, e) * AmperageFactor(amps, e);
        }

        /// Consumo de varilla (mm/s) proporcional a la corriente.
        public static float BurnOffMmPerSec(float amps, Electrode e)
            => e.BurnRateMmPerSec * (amps / Mathf.Max(1f, e.AmpNominal));

        // ---- Angulos de la herramienta ----------------------------------------
        // Con t = tangente de la costura (direccion de avance), n = normal de la junta,
        // y d = direccion del electrodo (desde la pieza hacia el mango):
        //   angulo de avance  = inclinacion de d dentro del plano (n,t)  -> ideal ~10-15 deg de arrastre
        //   angulo de trabajo = inclinacion de d dentro del plano (n,b), b = n x t -> ideal 0 (0 plano) o 45 (filete)

        public static void ToolAngles(Vector3 electrodeDir, Vector3 tangent, Vector3 normal,
                                      out float travelAngleDeg, out float workAngleDeg)
        {
            Vector3 n = normal.normalized;
            Vector3 t = Vector3.ProjectOnPlane(tangent, n).normalized;
            Vector3 b = Vector3.Cross(n, t).normalized;
            Vector3 d = electrodeDir.normalized;

            travelAngleDeg = Mathf.Atan2(Vector3.Dot(d, t), Vector3.Dot(d, n)) * Mathf.Rad2Deg;
            workAngleDeg   = Mathf.Atan2(Vector3.Dot(d, b), Vector3.Dot(d, n)) * Mathf.Rad2Deg;
        }

        /// Calidad geometrica 0..1 a partir del error angular respecto a la norma.
        /// Hasta 'tolDeg' no penaliza; despues cae linealmente hasta 3x la tolerancia.
        public static float AngleQuality(float travelDeg, float workDeg,
                                         float idealTravel, float idealWork, float tolDeg = 12f)
        {
            float et = Mathf.Abs(travelDeg - idealTravel);
            float ew = Mathf.Abs(workDeg   - idealWork);
            float e  = Mathf.Max(et, ew);
            if (e <= tolDeg) return 1f;
            return Mathf.Clamp01(1f - (e - tolDeg) / (2f * tolDeg));
        }

        // ---- Umbrales de energia ----------------------------------------------
        /// Energia (J) para fundir correctamente un punto, segun espesor de chapa.
        /// Calibrado para que a corriente nominal la ventana buena sea del orden de
        /// decimas de segundo por punto (velocidad de avance realista).
        public static void Thresholds(float thicknessMm, out float coldMax, out float burnMin)
        {
            // Calibrados (ver validacion en PROYECTO_SOLDADURA.md): con estos valores la
            // velocidad de avance correcta cae en 3-6 mm/s, que es el rango real de SMAW.
            coldMax = 220f * thicknessMm;
            burnMin = 1100f * thicknessMm;
        }

        /// Clasificacion final de un punto del cordon.
        /// angleQ y arcQ son promedios ponderados por energia (0..1) acumulados al depositar.
        public static WeldQuality Classify(float energyJ, float thicknessMm, float angleQ, float arcQ)
        {
            Thresholds(thicknessMm, out float coldMax, out float burnMin);
            if (energyJ <= 1f)        return WeldQuality.None;
            if (energyJ < coldMax)    return WeldQuality.Cold;
            if (energyJ > burnMin)    return WeldQuality.BurnThrough;
            if (angleQ  < 0.45f)      return WeldQuality.Undercut;
            if (arcQ    < 0.40f)      return WeldQuality.Porosity;
            return WeldQuality.Good;
        }

        public static Color QualityColor(WeldQuality q)
        {
            switch (q)
            {
                case WeldQuality.Good:        return new Color(0.72f, 0.72f, 0.75f); // gris metal limpio
                case WeldQuality.Cold:        return new Color(0.35f, 0.55f, 0.85f); // azulado, falto de fusion
                case WeldQuality.Undercut:    return new Color(0.85f, 0.60f, 0.20f); // naranja
                case WeldQuality.Porosity:    return new Color(0.55f, 0.45f, 0.30f); // marron poroso
                case WeldQuality.BurnThrough: return new Color(0.85f, 0.15f, 0.10f); // rojo
                default:                      return new Color(0.4f, 0.4f, 0.4f);
            }
        }

        public static string QualityLabel(WeldQuality q)
        {
            switch (q)
            {
                case WeldQuality.Good:        return "Correcto";
                case WeldQuality.Cold:        return "Falta de fusion";
                case WeldQuality.Undercut:    return "Socavado (angulo)";
                case WeldQuality.Porosity:    return "Porosidad (arco largo)";
                case WeldQuality.BurnThrough: return "Perforacion";
                default:                      return "Sin soldar";
            }
        }
    }
}
