using UnityEngine;

namespace FrankaVR.Welding
{
    public enum JointType
    {
        BeadOnPlate, // cordon sobre chapa: sin junta, solo trazo
        ButtJoint,   // junta a tope entre dos chapas
        FilletT,     // junta en T (filete), angulo de trabajo 45 grados
        LapJoint,    // solape de chapa fina
        PipeButt,    // junta circunferencial de caño
        BracketFillet // filete de la mensula con cartelas (SeamIndex elige la costura)
    }

    public enum SeamShape { Line, Circle }

    /// Una "orden de trabajo" = un ejercicio del simulador. Define la geometria de la
    /// pieza, la costura, la norma de angulos y la regulacion recomendada.
    /// Es lo que hace del proyecto un SIMULADOR DE ENTRENAMIENTO y no una demo de movimiento.
    [System.Serializable]
    public class WorkOrder
    {
        public string Id = "N1";
        public string Title = "Cordon sobre chapa";
        public string Brief = "";
        public JointType Joint = JointType.BeadOnPlate;
        public SeamShape Shape = SeamShape.Line;

        [Header("Pieza")]
        public float ThicknessMm = 6f;
        public float SeamLengthM = 0.20f; // largo de la costura (o circunferencia si es caño)
        public float PipeRadiusM = 0.06f; // solo para PipeButt
        public int SeamIndex = 0;         // solo para BracketFillet

        [Header("Norma de angulos (grados)")]
        public float IdealTravelAngle = 12f; // arrastre tipico
        public float IdealWorkAngle   = 0f;  // 45 para filete
        public float AngleToleranceDeg = 12f;

        [Header("Regulacion recomendada")]
        public string RecommendedElectrode = "E6013 3.25";
        public float  RecommendedAmps = 110f;
        [Tooltip("Velocidad de avance programada para la ejecucion automatica (mm/s).")]
        public float  RecommendedSpeedMmS = 6f;

        [Header("Evaluacion")]
        public float MinScoreToPass = 70f;
        public float TimeLimitSec = 180f;

        /// Trabajos actuales: las tres costuras de filete de una mensula con cartelas.
        /// La normal de cada costura es la bisectriz de la esquina: angulo de trabajo ideal 0.
        public static WorkOrder[] Catalog => new[]
        {
            new WorkOrder{
                Id="M1", Title="Filete cartela izquierda", Joint=JointType.BracketFillet, Shape=SeamShape.Line, SeamIndex=1,
                Brief="Filete entre la cartela izquierda y la base, del lado de adentro. Se suelda desde la placa hacia la punta de la cartela, con la torcha inclinada hacia la cartela.",
                ThicknessMm=8f, SeamLengthM=0.06f,
                IdealTravelAngle=10f, IdealWorkAngle=0f, AngleToleranceDeg=10f,
                RecommendedElectrode="ER70S-6 1.0 · Ar/CO2", RecommendedAmps=150f, RecommendedSpeedMmS=6f,
                MinScoreToPass=70f, TimeLimitSec=0f },

            new WorkOrder{
                Id="M2", Title="Filete cartela derecha", Joint=JointType.BracketFillet, Shape=SeamShape.Line, SeamIndex=2,
                Brief="Igual que la izquierda, en la otra cartela: la torcha se inclina hacia el lado contrario.",
                ThicknessMm=8f, SeamLengthM=0.06f,
                IdealTravelAngle=10f, IdealWorkAngle=0f, AngleToleranceDeg=10f,
                RecommendedElectrode="ER70S-6 1.0 · Ar/CO2", RecommendedAmps=150f, RecommendedSpeedMmS=6f,
                MinScoreToPass=70f, TimeLimitSec=0f },

            new WorkOrder{
                Id="M3", Title="Filete frontal placa-base", Joint=JointType.BracketFillet, Shape=SeamShape.Line, SeamIndex=0,
                Brief="Avanzado: filete entre la base y la placa vertical, entre las dos cartelas. La torcha apunta hacia el robot; usar VISTA si el brazo tapa la junta.",
                ThicknessMm=8f, SeamLengthM=0.10f,
                IdealTravelAngle=10f, IdealWorkAngle=0f, AngleToleranceDeg=10f,
                RecommendedElectrode="ER70S-6 1.0 · Ar/CO2", RecommendedAmps=160f, RecommendedSpeedMmS=6f,
                MinScoreToPass=70f, TimeLimitSec=0f },
        };

        /// Ejercicios N1-N5 de la version con electrodo revestido, conservados como referencia.
        public static WorkOrder[] LegacyCatalog => new[]
        {
            new WorkOrder{
                Id="N1", Title="Cordon sobre chapa (plana)", Joint=JointType.BeadOnPlate, Shape=SeamShape.Line,
                Brief="Trazar un cordon recto de 20 cm sobre chapa de 6 mm. Objetivo: mantener longitud de arco y velocidad de avance constantes.",
                ThicknessMm=6f, SeamLengthM=0.20f,
                IdealTravelAngle=12f, IdealWorkAngle=0f, AngleToleranceDeg=15f,
                RecommendedElectrode="E6013 3.25", RecommendedAmps=110f,
                MinScoreToPass=60f, TimeLimitSec=180f },

            new WorkOrder{
                Id="N2", Title="Junta a tope 6 mm", Joint=JointType.ButtJoint, Shape=SeamShape.Line,
                Brief="Unir dos chapas de 6 mm a tope. Hay que fundir ambos bordes sin caerse por la raiz.",
                ThicknessMm=6f, SeamLengthM=0.20f,
                IdealTravelAngle=12f, IdealWorkAngle=0f, AngleToleranceDeg=12f,
                RecommendedElectrode="E6013 3.25", RecommendedAmps=115f,
                MinScoreToPass=65f, TimeLimitSec=180f },

            new WorkOrder{
                Id="N3", Title="Junta en T (filete)", Joint=JointType.FilletT, Shape=SeamShape.Line,
                Brief="Filete horizontal en junta en T de 8 mm. El angulo de trabajo debe ser 45 grados: si se desvia, el cordon se corre a una sola chapa y socava.",
                ThicknessMm=8f, SeamLengthM=0.20f,
                IdealTravelAngle=10f, IdealWorkAngle=45f, AngleToleranceDeg=10f,
                RecommendedElectrode="E7018 3.25", RecommendedAmps=125f,
                MinScoreToPass=70f, TimeLimitSec=200f },

            new WorkOrder{
                Id="N4", Title="Solape en chapa fina 2 mm", Joint=JointType.LapJoint, Shape=SeamShape.Line,
                Brief="Chapa de 2 mm: la ventana termica es minima. Con exceso de corriente o de permanencia se perfora en menos de un segundo.",
                ThicknessMm=2f, SeamLengthM=0.16f,
                IdealTravelAngle=15f, IdealWorkAngle=0f, AngleToleranceDeg=10f,
                RecommendedElectrode="E6013 2.5", RecommendedAmps=75f,
                MinScoreToPass=70f, TimeLimitSec=150f },

            new WorkOrder{
                Id="N5", Title="Caño a tope (posicion fija)", Joint=JointType.PipeButt, Shape=SeamShape.Circle,
                Brief="Junta circunferencial en caño de 120 mm. El angulo de trabajo debe girar de forma continua siguiendo la circunferencia: es el ejercicio que realmente exige control 3D de la orientacion.",
                ThicknessMm=5f, PipeRadiusM=0.06f,
                IdealTravelAngle=10f, IdealWorkAngle=0f, AngleToleranceDeg=10f,
                RecommendedElectrode="E7018 3.25", RecommendedAmps=120f,
                MinScoreToPass=70f, TimeLimitSec=240f },
        };

        public static WorkOrder ById(string id)
        {
            var c = Catalog;
            for (int i = 0; i < c.Length; i++) if (c[i].Id == id) return c[i];
            return c[0];
        }
    }
}
