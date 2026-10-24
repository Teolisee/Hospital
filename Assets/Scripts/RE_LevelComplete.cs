using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestiona la victoria del nivel, estadisticas finales y transicion a la siguiente escena.
/// La victoria es disparada de forma EXCLUSIVA por el NPC de Facturacion (Recepcionista)
/// despues de completar las 3 tareas de revision con el Guardia, el Civil y el Enfermero.
/// </summary>
public class RE_LevelComplete : MonoBehaviour 
{
    public static RE_LevelComplete Instance { get; private set; }

    [Header("UI y Paneles")]
    [Tooltip("Panel o Canvas que muestra la pantalla de victoria.")]
    [SerializeField] private GameObject levelCompletePanel;

    [Tooltip("Texto para mostrar las tareas completadas.")]
    [SerializeField] private TextMeshProUGUI tasksText;

    [Tooltip("Texto para mostrar la salud o tiempo restante.")]
    [SerializeField] private TextMeshProUGUI healthText;

    [Tooltip("Texto para mostrar el tiempo total jugado.")]
    [SerializeField] private TextMeshProUGUI timeText;

    [Header("Botones de Navegacion")]
    [Tooltip("Boton para pasar al siguiente nivel.")]
    [SerializeField] private Button nextLevelButton;

    [Tooltip("Boton para reiniciar el nivel actual.")]
    [SerializeField] private Button restartButton;

    [Tooltip("Boton para volver al menu principal.")]
    [SerializeField] private Button mainMenuButton;

    [Header("Configuracion de Escenas")]
    [Tooltip("Nombre de la escena del siguiente nivel (si esta vacio se usa Nivel 2 o Victoria).")]
    [SerializeField] private string nextLevelSceneName = "";

    [Tooltip("Nombre de la escena del menu principal.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Efectos Opcionales")]
    [Tooltip("Efecto de sonido al completar el nivel.")]
    [SerializeField] private AudioClip completeSFX;

    [Tooltip("AudioSource para reproducir efectos.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Sistema de particulas a activar.")]
    [SerializeField] private ParticleSystem completeParticles;

    private float levelStartTime;
    private bool isLevelCompleted = false;

    private void Awake() 
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        levelStartTime = Time.time;

        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(false);
        }

        DetectButtons();
    }

    private void DetectButtons()
    {
        if (levelCompletePanel == null) return;

        Button[] buttons = levelCompletePanel.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            string name = btn.gameObject.name.ToLower();
            if (nextLevelButton == null && (name.Contains("next") || name.Contains("siguiente") || name.Contains("continuar")))
            {
                nextLevelButton = btn;
            }
            else if (restartButton == null && (name.Contains("restart") || name.Contains("reiniciar") || name.Contains("repetir")))
            {
                restartButton = btn;
            }
            else if (mainMenuButton == null && (name.Contains("menu") || name.Contains("principal") || name.Contains("salir") || name.Contains("home")))
            {
                mainMenuButton = btn;
            }
        }
    }

    private void Start()
    {
        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.RemoveAllListeners();
            nextLevelButton.onClick.AddListener(NextLevel);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveAllListeners();
            restartButton.onClick.AddListener(RestartLevel);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveAllListeners();
            mainMenuButton.onClick.AddListener(LoadMainMenu);
        }
    }

    public bool IsLevelCompleted() 
    {
        return isLevelCompleted; 
    }

    /// <summary>
    /// Ejecuta la secuencia de victoria y muestra la pantalla final.
    /// Invocado EXCLUSIVAMENTE por la Recepcionista tras validar los 3 NPCs previos.
    /// </summary>
    public void TriggerLevelComplete() 
    {
        if (isLevelCompleted) return; 
        isLevelCompleted = true;

        Debug.Log("[RE_LevelComplete] ¡Nivel completado con exito via Recepcionista!");

        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(true);
        }

        Cursor.lockState = CursorLockMode.None; 
        Cursor.visible = true; 

        // Detener salud/temporizador y movimiento del jugador
        if (RE_PlayerHealth.Instance != null) RE_PlayerHealth.Instance.SetPaused(true);
        RE_PlayerMovement pm = FindFirstObjectByType<RE_PlayerMovement>();
        if (pm != null) pm.enabled = false;

        CalcularYMostrarEstadisticas();

        if (completeParticles != null) completeParticles.Play();

        if (completeSFX != null) 
        {
            if (audioSource == null) 
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.PlayOneShot(completeSFX); 
        }
    }

    private void CalcularYMostrarEstadisticas() 
    {
        if (timeText != null) 
        {
            float elapsed = Time.time - levelStartTime;
            int minutes = Mathf.FloorToInt(elapsed / 60F);
            int seconds = Mathf.FloorToInt(elapsed - minutes * 60);
            timeText.text = string.Format("Tiempo: {0:00}:{1:00}", minutes, seconds); 
        }

        if (RE_GameProgress.Instance != null && tasksText != null) 
        {
            int completed = RE_GameProgress.Instance.progressData.completedTasks.Count; 
            int total = RE_GameProgress.Instance.totalMainTasks; 
            tasksText.text = string.Format("Tareas: {0} / {1}", completed, total); 
        }

        if (healthText != null) 
        {
            healthText.text = "Salud: Completo"; 
        }
    }

    public void NextLevel() 
    {
        Time.timeScale = 1f;

        string currentScene = SceneManager.GetActiveScene().name;
        string targetScene = nextLevelSceneName;

        if (string.IsNullOrEmpty(targetScene))
        {
            if (currentScene == "Nivel 1" || currentScene == "Hospital_Lobby")
            {
                targetScene = Application.CanStreamedLevelBeLoaded("Nivel 2") ? "Nivel 2" : "Hospital_Level2";
            }
            else if (currentScene == "Nivel 2" || currentScene == "Hospital_Level2")
            {
                targetScene = Application.CanStreamedLevelBeLoaded("Victoria") ? "Victoria" : "MainMenu";
            }
            else
            {
                targetScene = "MainMenu";
            }
        }

        Debug.Log($"[RE_LevelComplete] Cargando siguiente escena: '{targetScene}'");

        if (RE_LevelTransitionManager.Instance != null)
        {
            RE_LevelTransitionManager.Instance.TransitionToScene(targetScene);
        }
        else
        {
            SceneManager.LoadScene(targetScene);
        }
    }

    public void RestartLevel() 
    {
        Time.timeScale = 1f;
        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"[RE_LevelComplete] Reiniciando escena: '{currentScene}'");

        if (RE_LevelTransitionManager.Instance != null)
        {
            RE_LevelTransitionManager.Instance.TransitionToScene(currentScene);
        }
        else
        {
            SceneManager.LoadScene(currentScene);
        }
    }

    public void LoadMainMenu() 
    {
        Time.timeScale = 1f;
        Debug.Log($"[RE_LevelComplete] Cargando Menu Principal: '{mainMenuSceneName}'");

        if (RE_LevelTransitionManager.Instance != null)
        {
            RE_LevelTransitionManager.Instance.TransitionToScene(mainMenuSceneName);
        }
        else
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void OnDestroy() 
    {
        if (Instance == this) 
        {
            Instance = null; 
        }
    }
}
