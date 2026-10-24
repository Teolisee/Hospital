using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[System.Serializable]
public class GameProgressData 
{
    [Header("Datos Generales")]
    public string currentSceneName = "Nivel 1";
    public int RE_PlayerHealth = 100;
    public int playerMaxHealth = 100;

    [Header("Progreso del Juego")]
    public List<string> completedTasks = new List<string>();
    public List<string> keycardsObtained = new List<string>();
    public int currentObjectiveIndex = 0;

    [Header("Posición del Jugador")]
    public bool hasSavedPosition = false;
    public float playerPosX;
    public float playerPosY;
    public float playerPosZ;

    [Header("Hitos de Progreso")]
    public bool reached25 = false; 
    public bool reached50 = false; 
    public bool reached75 = false; 
    public bool reached100 = false;

    // Propiedad cómoda para código moderno manteniendo retrocompatibilidad
    public int playerHealth
    {
        get => RE_PlayerHealth;
        set => RE_PlayerHealth = value;
    }
}

/// <summary>
/// Gestiona los datos globales de progreso, persistencia en PlayerPrefs (JSON)
/// y sincronización con el HUD de objetivos y porcentaje.
/// </summary>
public class RE_GameProgress : MonoBehaviour 
{
    public static RE_GameProgress Instance { get; private set; }

    [Header("Datos de Progreso")]
    public GameProgressData progressData = new GameProgressData(); 

    [Header("Ajustes de Porcentaje")]
    [Tooltip("Cantidad total de misiones principales necesarias para llegar al 100%.")]
    public int totalMainTasks = 4;

    [Header("Herramientas de Prueba")]
    [Tooltip("Marcar para borrar la partida guardada y empezar desde cero al iniciar.")]
    public bool reiniciarAlIniciar = false;

    [Header("Referencias de UI (HUD)")]
    [SerializeField] private GameObject progressTextObject; 
    [SerializeField] private GameObject progressBarObject; 

    // Eventos y delegados para notificar cambios de progreso
    public static event System.Action<float> OnProgressChanged;
    public static event System.Action OnProgress25;
    public static event System.Action OnProgress50;
    public static event System.Action OnProgress75;
    public static event System.Action OnProgress100;

    [Header("Configuración de Guardado")]
    [SerializeField] private string saveKey = "HospitalGameProgress";

    private void Awake()
    {
        // IMPORTANTE: Este componente vive dentro del Canvas del HUD ("Hud Progreso y jugador").
        // NO se debe desacoplar (SetParent) ni marcar DontDestroyOnLoad, porque eso saca el HUD
        // del Canvas y deja de dibujarse. El progreso persiste entre escenas vía PlayerPrefs.
        Instance = this;

        if (reiniciarAlIniciar)
        {
            ResetProgress();
        }
        else
        {
            LoadProgress();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DetectUIComponents();
        ActualizarUI(GetProgressPercentage());
    }

    private void Start()
    {
        DetectUIComponents(); 
        ActualizarUI(GetProgressPercentage()); 
    }

    /// <summary>
    /// Localiza de forma eficiente los componentes de UI del HUD en la escena actual.
    /// </summary>
    public void DetectUIComponents()
    {
        // 1. Buscar texto de porcentaje si la referencia es nula o se perdió en el cambio de escena
        if (progressTextObject == null)
        {
            GameObject directObj = GameObject.Find("Porcentaje");
            if (directObj != null)
            {
                progressTextObject = directObj;
            }
            else
            {
                TextMeshProUGUI[] tmps = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var t in tmps)
                {
                    string n = t.gameObject.name.ToLower();
                    if (n.Contains("porcentaje") || n.Contains("progreso"))
                    {
                        progressTextObject = t.gameObject;
                        break;
                    }
                }
            }
        }

        // 2. Buscar barra de progreso
        if (progressBarObject == null)
        {
            GameObject directBar = GameObject.Find("Barra de progreso");
            if (directBar != null)
            {
                progressBarObject = directBar;
            }
            else
            {
                Slider[] sliders = FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var s in sliders)
                {
                    string n = s.gameObject.name.ToLower();
                    if (n.Contains("progreso") || n.Contains("progressbar") || n.Contains("barra"))
                    {
                        progressBarObject = s.gameObject;
                        break;
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
            if (progressData == null) progressData = new GameProgressData();

            string json = JsonUtility.ToJson(progressData, true);
            PlayerPrefs.SetString(saveKey, json);
            PlayerPrefs.Save();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RE_GameProgress] Error al guardar el progreso: {e.Message}");
        }
    }

    /// <summary>
    /// Carga la partida guardada desde PlayerPrefs.
    /// </summary>
    public void LoadProgress() 
    {
        if (PlayerPrefs.HasKey(saveKey))
        {
            try
            {
                string json = PlayerPrefs.GetString(saveKey);
                if (progressData == null) progressData = new GameProgressData();
                JsonUtility.FromJsonOverwrite(json, progressData);

                if (progressData.completedTasks == null)
                {
                    progressData.completedTasks = new List<string>();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RE_GameProgress] Partida corrupta detectada. Iniciando valores por defecto: {e.Message}");
                progressData = new GameProgressData();
            }
        }
        else
        {
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
        PlayerPrefs.DeleteKey("HospitalGameProgress");
        PlayerPrefs.DeleteKey("HospitalGam");
        PlayerPrefs.Save();

        progressData = new GameProgressData();
        ActualizarUI(0f);
        Debug.Log("[RE_GameProgress] Progreso reseteado a 0%.");
    }

    /// <summary>
    /// Borra las misiones del nivel actual manteniendo otros datos.
    /// </summary>
    public void ResetLevelProgressOnly()
    {
        if (progressData == null) progressData = new GameProgressData();

        progressData.completedTasks.Clear();
        progressData.reached25 = false;
        progressData.reached50 = false;
        progressData.reached75 = false;
        progressData.reached100 = false;

        ActualizarUI(0f);
        SaveProgress();
    }

    /// <summary>
    /// Registra una tarea como completada y dispara eventos.
    /// </summary>
    public void UpdateTaskProgress(string taskId, bool completed = true) { if (completed) CompleteTask(taskId); }

    public void CompleteTask(string taskId)
    {
        if (string.IsNullOrEmpty(taskId)) return;
        if (progressData == null) progressData = new GameProgressData();

        if (!progressData.completedTasks.Contains(taskId))
        {
            progressData.completedTasks.Add(taskId);
            
            float currentPercent = GetProgressPercentage();
            
            OnProgressChanged?.Invoke(currentPercent);
            CheckProgressMilestones(currentPercent);
            ActualizarUI(currentPercent);
            SaveProgress();

            Debug.Log($"[RE_GameProgress] Tarea completada: '{taskId}' ({progressData.completedTasks.Count}/{totalMainTasks} -> {currentPercent:0}%)");
        }
    }

    public float GetProgressPercentage()
    {
        if (progressData == null || progressData.completedTasks == null) return 0f;
        if (totalMainTasks <= 0) return 0f;
        float percent = ((float)progressData.completedTasks.Count / totalMainTasks) * 100f;
        return Mathf.Clamp(percent, 0f, 100f);
    }

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

    public bool IsTaskCompleted(string taskId) 
    { 
        if (string.IsNullOrEmpty(taskId) || progressData == null || progressData.completedTasks == null) return false;
        return progressData.completedTasks.Contains(taskId); 
    }

    public bool IsGuardiaCompleted() 
    { 
        if (progressData == null || progressData.completedTasks == null) return false;
        foreach (string task in progressData.completedTasks) 
            if (task.ToLower().Contains("guardia")) return true; 
        return false; 
    }

    public bool IsCivilCompleted() 
    { 
        if (progressData == null || progressData.completedTasks == null) return false;
        foreach (string task in progressData.completedTasks) 
            if (task.ToLower().Contains("civil")) return true; 
        return false; 
    }

    public bool IsEnfermeroCompleted() 
    { 
        if (progressData == null || progressData.completedTasks == null) return false;
        foreach (string task in progressData.completedTasks) 
            if (task.ToLower().Contains("enfermero")) return true; 
        return false; 
    }

    public void ActualizarUI(float porcentaje)
    {
        DetectUIComponents();

        string textoFormateado = $"Progreso {porcentaje:0}%";

        // 1. Texto de progreso
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

        // 2. Barra de progreso
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

    /// <summary>
    /// Limpia todos los suscriptores a eventos estáticos para evitar fugas de memoria.
    /// </summary>
    public static void ClearAllEventListeners()
    {
        OnProgressChanged = null;
        OnProgress25 = null;
        OnProgress50 = null;
        OnProgress75 = null;
        OnProgress100 = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
