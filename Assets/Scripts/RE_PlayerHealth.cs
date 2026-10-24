using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

/// <summary>
/// Administra la salud y el temporizador de supervivencia del jugador.
/// Sincroniza la barra de vida, textos en pantalla y gestiona la derrota si el tiempo o la salud se agotan.
/// Optimizado para evitar llamadas a disco en Update y asegurar temporización precisa.
/// </summary>
public class RE_PlayerHealth : MonoBehaviour 
{
    public static RE_PlayerHealth Instance { get; private set; }

    [Header("Referencias de UI (Interfaz)")] 
    public Slider healthSlider;
    public Image healthImageFill;
    public TextMeshProUGUI healthText;

    [Header("Modo Temporizador")]
    [Tooltip("Si está activo, la vida se reduce progresivamente como un reloj de cuenta regresiva.")]
    public bool useAsTimer = true;

    [Tooltip("Duración límite del nivel en segundos.")]
    public float levelDuration = 59f;

    [Header("Configuración de Derrota")]
    [Tooltip("Nombre de la escena de fin del juego.")]
    public string gameOverSceneName = "Fin del Juego";

    private float currentSecondsRemaining;
    private bool isPaused = false;
    private bool isDead = false;
    private int lastRecordedHealth = -1;

    public void SetPaused(bool paused) => isPaused = paused;

    private void Awake() 
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start() 
    {
        isDead = false;
        int max = GetMaxHealth();

        if (useAsTimer)
        {
            currentSecondsRemaining = levelDuration > 0 ? levelDuration : 59f;
        }
        else
        {
            currentSecondsRemaining = max;
        }

        // Inicializar salud al máximo en memoria
        SetMemoryHealth(max);
        UpdateHealthUI();
    }

    private void Update() 
    {
        if (isDead) return;

        // Si el nivel ya fue completado, congelar la pérdida de salud
        if (RE_LevelComplete.Instance != null && RE_LevelComplete.Instance.IsLevelCompleted())
        {
            return;
        }

        if (useAsTimer && !isPaused) 
        {
            currentSecondsRemaining -= Time.deltaTime;

            if (currentSecondsRemaining <= 0f)
            {
                currentSecondsRemaining = 0f;
                SetMemoryHealth(0);
                UpdateHealthUI();
                Die();
                return;
            }

            // Calcular salud proporcional al tiempo restante
            int maxHealth = GetMaxHealth();
            float ratio = Mathf.Clamp01(currentSecondsRemaining / (levelDuration > 0 ? levelDuration : 1f));
            int calculatedHealth = Mathf.CeilToInt(ratio * maxHealth);

            // Actualizar memoria solo cuando cambia el número entero
            if (calculatedHealth != lastRecordedHealth)
            {
                SetMemoryHealth(calculatedHealth);
            }

            UpdateHealthUI();
        }
    }

    /// <summary>
    /// Aplica daño directo al jugador.
    /// </summary>
    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0) return;

        int current = GetCurrentHealth() - damage;
        current = Mathf.Clamp(current, 0, GetMaxHealth());
        SetMemoryHealth(current);

        if (useAsTimer && levelDuration > 0)
        {
            currentSecondsRemaining = ((float)current / GetMaxHealth()) * levelDuration;
        }

        UpdateHealthUI();

        if (current <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Actualiza barras y textos visuales de salud y tiempo en pantalla.
    /// </summary>
    public void UpdateHealthUI() 
    {
        int max = GetMaxHealth();
        int current = GetCurrentHealth();
        float percent = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;

        if (healthSlider != null) 
        { 
            healthSlider.maxValue = max; 
            healthSlider.value = current; 
        }

        if (healthImageFill != null) 
        {
            healthImageFill.fillAmount = percent;
        }

        if (healthText != null)
        {
            if (useAsTimer) 
            {
                int min = Mathf.FloorToInt(currentSecondsRemaining / 60f);
                int sec = Mathf.FloorToInt(currentSecondsRemaining % 60f);
                healthText.text = string.Format("Tiempo: {0:00}:{1:00}", min, sec); 
            }
            else 
            {
                healthText.text = $"Salud: {current}/{max}";
            }
        }
    }

    private void Die() 
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("[RE_PlayerHealth] El jugador ha perdido toda su salud o tiempo.");

        // Restaurar salud al máximo para el siguiente intento
        int max = GetMaxHealth();
        SetMemoryHealth(max);

        // Guardar progreso definitivo en disco solo al morir
        if (RE_GameProgress.Instance != null)
        {
            RE_GameProgress.Instance.SaveProgress();
        }

        string targetScene = !string.IsNullOrEmpty(gameOverSceneName) ? gameOverSceneName : "Fin del Juego";

        if (RE_LevelTransitionManager.Instance != null) 
        {
            RE_LevelTransitionManager.Instance.TransitionToScene(targetScene);
        }
        else 
        {
            SceneManager.LoadScene(RE_LevelTransitionManager.ResolveValidSceneName(targetScene));
        }
    }

    public int GetCurrentHealth()
    {
        if (RE_GameProgress.Instance != null && RE_GameProgress.Instance.progressData != null)
        {
            return RE_GameProgress.Instance.progressData.RE_PlayerHealth;
        }
        return lastRecordedHealth >= 0 ? lastRecordedHealth : 100;
    }

    private int GetMaxHealth() 
    {
        if (RE_GameProgress.Instance != null && RE_GameProgress.Instance.progressData != null)
        {
            int max = RE_GameProgress.Instance.progressData.playerMaxHealth;
            return max > 0 ? max : 100;
        }
        return 100;
    }

    private void SetMemoryHealth(int amount)
    {
        lastRecordedHealth = amount;
        if (RE_GameProgress.Instance != null && RE_GameProgress.Instance.progressData != null)
        {
            RE_GameProgress.Instance.progressData.RE_PlayerHealth = amount;
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
