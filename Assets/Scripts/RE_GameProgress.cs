using UnityEngine; // La caja de herramientas básica de Unity.
using System.Collections.Generic; // Nos permite crear "Listas" (mochilas donde guardamos muchos datos juntos).
using TMPro; // Herramienta para usar letras bonitas y nítidas (TextMeshPro).
using UnityEngine.UI; // Herramienta para usar barras de vida o botones.
using UnityEngine.SceneManagement; // El encargado de cambiar de mapas/niveles.

// -----------------------------------------------------------------------------
// CLASE: GameProgressData
// METÁFORA: "La Hoja de Vida / El Archivero"
// Esta clase no tiene código que haga acciones. Es literalmente un pedazo de papel 
// donde anotamos qué ha hecho el jugador para luego guardarlo en el disco duro.
// -----------------------------------------------------------------------------
[System.Serializable] // Permite que Unity "empaquete" estos datos en un archivo de texto (JSON)
public class GameProgressData 
{
    [Header("Datos Generales")]
    public string currentSceneName = "Hospital_Lobby"; // En qué piso del hospital se quedó.
    public int RE_PlayerHealth = 100; // Con cuánta vida se fue a dormir.
    public int playerMaxHealth = 100; // Cuánta vida máxima puede tener.

    [Header("Progreso del Juego")]
    public List<string> completedTasks = new List<string>(); // La lista del mercado: "Ya hablé con el Guardia", "Ya hablé con el Civil", etc.
    public List<string> keycardsObtained = new List<string>(); // Llavero para guardar tarjetas de acceso.
    public int currentObjectiveIndex = 0; // Por qué paso de la misión vamos (0, 1, 2...).

    [Header("Posición del Jugador")]
    public bool hasSavedPosition = false; // ¿Guardamos la ubicación exacta en el mapa?
    public float playerPosX; // Coordenada X (Derecha/Izquierda)
    public float playerPosY; // Coordenada Y (Arriba/Abajo)
    public float playerPosZ; // Coordenada Z (Adelante/Atrás)

    [Header("Hitos de Progreso")]
    // Como las insignias o trofeos. ¿Ya llegó a cierto porcentaje del nivel?
    public bool reached25 = false; 
    public bool reached50 = false; 
    public bool reached75 = false; 
    public bool reached100 = false; 
}

// -----------------------------------------------------------------------------
// SCRIPT PRINCIPAL: RE_GameProgress
// METÁFORA: "El Alcalde del Juego / El Cerebro"
// Este script administra la hoja de vida (GameProgressData) y toma las decisiones.
// -----------------------------------------------------------------------------
public class RE_GameProgress : MonoBehaviour 
{
    // -------------------------------------------------------------------------
    // EXPLICACIÓN DE 'SINGLETON' (Instance) PARA TU SUSTENTACIÓN:
    // Imagina que este script es el Alcalde de la ciudad. Solo puede haber un alcalde.
    // Si un ciudadano (otro script) quiere hablar con él, no tiene que ir preguntando 
    // casa por casa dónde vive. Simplemente llama a "La Oficina del Alcalde" (Instance) de forma directa.
    // Esto hace que el código sea rapidísimo porque todos saben exactamente dónde encontrar el progreso del juego.
    // -------------------------------------------------------------------------
    public static RE_GameProgress Instance { get; private set; }

    [Header("Datos de Progreso")]
    // Creamos nuestra hoja de papel en blanco usando la clase de arriba.
    public GameProgressData progressData = new GameProgressData(); 

    [Header("Ajustes de Porcentaje")]
    [Tooltip("Cantidad total de misiones principales necesarias para llegar al 100%.")]
    public int totalMainTasks = 4; // Por defecto son 4 NPCs (Guardia, Civil, Enfermero, Recepcionista).

    [Header("Herramientas de Prueba")]
    [Tooltip("Dale a Play con esto marcado para borrar la partida guardada y empezar de 0.")]
    public bool reiniciarAlIniciar = false; // Trampa para que los programadores prueben el juego desde cero.

    [Header("Referencias de UI (HUD)")]
    // Estos son los enlaces a los objetos gráficos de la pantalla (El 100% y la barra de progreso).
    [SerializeField] private GameObject progressTextObject; 
    [SerializeField] private GameObject progressBarObject; 

    // -------------------------------------------------------------------------
    // EXPLICACIÓN DE DELEGADOS Y EVENTOS (Action):
    // Imagina que esto es una "alarma de incendios". El Alcalde (este script) jala la alarma.
    // No sabe quién va a escucharla (puede ser el script de música, de logros, etc.), 
    // pero él cumple con gritar: "¡LLEGAMOS AL 100%!" y los demás actúan solos.
    // -------------------------------------------------------------------------
    public static event System.Action<float> OnProgressChanged;
    public static event System.Action OnProgress25;
    public static event System.Action OnProgress50;
    public static event System.Action OnProgress75;
    public static event System.Action OnProgress100;

    [Header("Configuración de Guardado")]
    [Tooltip("Clave con la que se guardará el archivo en el sistema.")]
    [SerializeField] private string saveKey = "HospitalGameProgress"; // El nombre de la carpeta virtual donde se guardará.

    private void Awake() // Awake se ejecuta antes de que el juego siquiera respire (antes de Start).
    {
        if (Instance == null) // Si todavía no hay ningún "Alcalde"...
        {
            Instance = this; // ¡Yo seré el Alcalde!
            
            // "DontDestroyOnLoad": Le pone un campo de fuerza a nuestro Alcalde para que sobreviva y pase al siguiente nivel.
            DontDestroyOnLoad(gameObject); 
            
            if (reiniciarAlIniciar) ResetProgress(); // Si el programador activó la trampa, limpiamos la partida.
            else LoadProgress(); // Si no, cargamos la partida desde el disco duro.
        }
        else // Si ya existía otro Alcalde (porque venimos de otro nivel y ya había uno)...
        {
            if (reiniciarAlIniciar) Instance.ResetProgress(); 
            Destroy(gameObject); // Nos destruimos a nosotros mismos porque no puede haber 2 Alcaldes.
        }
    }

    private void OnEnable() // Cuando este script despierta...
    {
        SceneManager.sceneLoaded += OnSceneLoaded; // Le decimos a Unity: "Avísame cada vez que termines de cargar un nivel nuevo".
    }

    private void OnDisable() // Cuando apagamos el script...
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // Dejamos de pedirle avisos a Unity.
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) // Esta función se llama sola cuando entramos a un nuevo nivel
    {
        DetectUIComponents(); // Volvemos a buscar dónde están los textos en la pantalla.
        ActualizarUI(GetProgressPercentage()); // Refrescamos los números.
    }

    private void Start() // Primer latido del juego
    {
        DetectUIComponents(); 
        ActualizarUI(GetProgressPercentage()); 
    }

    /// <summary>
    /// Función detective: Si olvidaste arrastrar los textos al Inspector, esta función los busca por todo el mapa.
    /// </summary>
    public void DetectUIComponents()
    {
        // 1. Buscamos el texto de porcentaje si está vacío o se perdió al cambiar de escena
        if (progressTextObject == null)
        {
            progressTextObject = GameObject.Find("Porcentaje");
            if (progressTextObject == null)
            {
                foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>()) 
                {
                    if (go.hideFlags == HideFlags.None && (go.name.ToLower().Contains("porcentaje") || go.name.ToLower().Contains("progreso")))
                    {
                        if (go.GetComponent<TextMeshProUGUI>() != null || go.GetComponentInChildren<TextMeshProUGUI>() != null ||
                            go.GetComponent<Text>() != null || go.GetComponentInChildren<Text>() != null)
                        {
                            progressTextObject = go;
                            break;
                        }
                    }
                }
            }
        }

        // 2. Buscamos la barra de progreso si está vacía o se perdió al cambiar de escena
        if (progressBarObject == null)
        {
            progressBarObject = GameObject.Find("Barra de progreso");
            if (progressBarObject == null)
            {
                foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (go.hideFlags == HideFlags.None && (go.name.ToLower().Contains("barra de progreso") || go.name.ToLower().Contains("progressbar") || go.name.ToLower().Contains("progress bar")))
                    {
                        if (go.GetComponent<Slider>() != null || go.GetComponentInChildren<Slider>() != null ||
                            go.GetComponent<Image>() != null || go.GetComponentInChildren<Image>() != null)
                        {
                            progressBarObject = go;
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Guarda la partida en PlayerPrefs usando formato JSON.
    /// </summary>
    public void SaveProgress() 
    {
        try
        {
            string json = JsonUtility.ToJson(progressData, true);
            PlayerPrefs.SetString(saveKey, json);
            PlayerPrefs.Save();
            Debug.Log("[RE_GameProgress] Progreso guardado con éxito.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RE_GameProgress] Error al guardar el progreso: {e.Message}");
        }
    }

    /// <summary>
    /// Carga la partida desde PlayerPrefs.
    /// </summary>
    public void LoadProgress() 
    {
        if (PlayerPrefs.HasKey(saveKey))
        {
            try
            {
                string json = PlayerPrefs.GetString(saveKey);
                JsonUtility.FromJsonOverwrite(json, progressData);
                Debug.Log($"[RE_GameProgress] Progreso cargado con éxito. Tareas completadas: {progressData.completedTasks.Count}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RE_GameProgress] Partida corrupta. Iniciando por defecto: {e.Message}");
                progressData = new GameProgressData();
            }
        }
        else
        {
            Debug.Log("[RE_GameProgress] No se encontró partida guardada. Iniciando nueva.");
            progressData = new GameProgressData();
        }
        ActualizarUI(GetProgressPercentage());
    }

    /// <summary>
    /// Borra definitivamente la partida guardada (Hard Reset).
    /// </summary>
    public void ResetProgress()
    {
        PlayerPrefs.DeleteKey(saveKey);
        progressData = new GameProgressData();
        ActualizarUI(0f);
        Debug.Log("[RE_GameProgress] Progreso reseteado a 0%.");
    }

    /// <summary>
    /// Borra las misiones del nivel actual manteniendo otros datos (Soft Reset).
    /// </summary>
    public void ResetLevelProgressOnly()
    {
        progressData.completedTasks.Clear();
        progressData.reached25 = false;
        progressData.reached50 = false;
        progressData.reached75 = false;
        progressData.reached100 = false;

        ActualizarUI(0f);
        SaveProgress();
        Debug.Log("[RE_GameProgress] Progreso de tareas del nivel reseteado a 0.");
    }

    #region Métodos de Utilidad / Atajos (Lógica de Misiones)

    /// <summary>
    /// Registra una tarea como completada y actualiza automáticamente el porcentaje y la interfaz.
    /// </summary>
    public void CompleteTask(string taskId)
    {
        if (string.IsNullOrEmpty(taskId)) return;

        if (!progressData.completedTasks.Contains(taskId))
        {
            progressData.completedTasks.Add(taskId);
            
            float currentPercent = GetProgressPercentage();
            
            OnProgressChanged?.Invoke(currentPercent);
            CheckProgressMilestones(currentPercent);
            ActualizarUI(currentPercent);
            SaveProgress();

            Debug.Log($"[RE_GameProgress] ✓ Tarea completada: '{taskId}'. Total: {progressData.completedTasks.Count}/{totalMainTasks} ({currentPercent:0}%)");
        }
        else
        {
            Debug.Log($"[RE_GameProgress] La tarea '{taskId}' ya estaba completada anteriormente.");
        }
    }

    /// <summary>
    /// Calcula el porcentaje de avance (0 a 100) en base a las tareas completadas y el total.
    /// </summary>
    public float GetProgressPercentage()
    {
        if (totalMainTasks <= 0) return 0f;
        float percent = ((float)progressData.completedTasks.Count / totalMainTasks) * 100f;
        return Mathf.Clamp(percent, 0f, 100f);
    }

    /// <summary>
    /// Revisa si se alcanzaron los hitos del 25%, 50%, 75% o 100% y dispara sus eventos correspondientes.
    /// </summary>
    private void CheckProgressMilestones(float percentage)
    {
        if (percentage >= 25f && !progressData.reached25)
        {
            progressData.reached25 = true;
            OnProgress25?.Invoke();
        }
        if (percentage >= 50f && !progressData.reached50)
        {
            progressData.reached50 = true;
            OnProgress50?.Invoke();
        }
        if (percentage >= 75f && !progressData.reached75)
        {
            progressData.reached75 = true;
            OnProgress75?.Invoke();
        }
        if (percentage >= 100f && !progressData.reached100)
        {
            progressData.reached100 = true;
            OnProgress100?.Invoke();
        }
    }

    /// <summary>
    /// Devuelve si una tarea específica con el taskId dado ya fue completada.
    /// </summary>
    public bool IsTaskCompleted(string taskId) 
    { 
        if (string.IsNullOrEmpty(taskId)) return false;
        return progressData.completedTasks.Contains(taskId); 
    }

    // Métodos de consulta amigables para saber si se ha interactuado con los diferentes roles
    public bool IsGuardiaCompleted() 
    { 
        foreach (string task in progressData.completedTasks) 
            if (task.ToLower().Contains("guardia")) return true; 
        return false; 
    }

    public bool IsCivilCompleted() 
    { 
        foreach (string task in progressData.completedTasks) 
            if (task.ToLower().Contains("civil")) return true; 
        return false; 
    }

    public bool IsEnfermeroCompleted() 
    { 
        foreach (string task in progressData.completedTasks) 
            if (task.ToLower().Contains("enfermero")) return true; 
        return false; 
    }

    /// <summary>
    /// Modifica los elementos visuales de la interfaz de usuario para mostrar el porcentaje actual.
    /// </summary>
    public void ActualizarUI(float porcentaje)
    {
        DetectUIComponents();

        string textoFormateado = $"Progreso {porcentaje:0}%";

        // 1. Actualizar texto de progreso (TextMeshPro o Legacy Text)
        if (progressTextObject != null)
        {
            TextMeshProUGUI textTMP = progressTextObject.GetComponent<TextMeshProUGUI>();
            if (textTMP == null) textTMP = progressTextObject.GetComponentInChildren<TextMeshProUGUI>();
            
            if (textTMP != null) 
            {
                textTMP.text = textoFormateado;
            }
            else
            {
                Text textLegacy = progressTextObject.GetComponent<Text>();
                if (textLegacy == null) textLegacy = progressTextObject.GetComponentInChildren<Text>();
                if (textLegacy != null) textLegacy.text = textoFormateado;
            }
        }

        // 2. Actualizar barra de progreso (Slider o Image Fill)
        if (progressBarObject != null)
        {
            Slider slider = progressBarObject.GetComponent<Slider>();
            if (slider == null) slider = progressBarObject.GetComponentInChildren<Slider>();
            
            if (slider != null)
            {
                slider.value = (porcentaje / 100f) * slider.maxValue;
            }
            else
            {
                Image image = progressBarObject.GetComponent<Image>();
                if (image == null) image = progressBarObject.GetComponentInChildren<Image>();
                
                if (image != null) 
                {
                    image.fillAmount = porcentaje / 100f;
                }
            }
        }
    }
    #endregion
}
