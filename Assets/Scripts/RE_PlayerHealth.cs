using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

// -----------------------------------------------------------------------------
// SCRIPT: RE_PlayerHealth
// METÁFORA: "El Médico del Jugador / El Cronómetro"
// Administra la vida del jugador y la actualiza en pantalla.
// -----------------------------------------------------------------------------
public class RE_PlayerHealth : MonoBehaviour 
{
    // Patrón Singleton: "El Alcalde" (Solo hay uno).
    public static RE_PlayerHealth Instance { get; private set; }

    [Header("Referencias de UI (Interfaz)")] 
    public Slider healthSlider; // Barra clásica
    public Image healthImageFill; // Barra redonda
    public TextMeshProUGUI healthText; // Textos en pantalla

    [Header("Modo Temporizador")]
    public bool useAsTimer = true; // ¿El tiempo te mata?
    public float levelDuration = 59f; // Segundos límite

    [Header("Configuración de Muerte")]
    public string gameOverSceneName = "Fin del Juego"; // Escena de derrota

    private float currentHealthTimer; // Cronómetro interno
    private bool isPaused = false; // Botón de pausa

    // Función que otros usan para pausar la pérdida de vida (ej: los NPCs al hablar)
    public void SetPaused(bool paused) => isPaused = paused;

    private void Awake() 
    {
        Instance = this;
    }

    private void Start() 
    {
        // Curar al jugador al 100% al iniciar
        int max = GetMaxHealth();
        currentHealthTimer = max;
        SaveHealth((int)currentHealthTimer);
        UpdateHealthUI();
    }

    private void Update() 
    {
        // 1. EL RELOJ DE ARENA: Pierdes vida con el tiempo.
        if (useAsTimer && !isPaused) 
        {
            if (RE_LevelComplete.Instance != null && RE_LevelComplete.Instance.IsLevelCompleted()) return;

            // Restamos la vida exacta calculada por cada segundo real que pasa.
            float decreaseRate = (float)GetMaxHealth() / levelDuration; 
            currentHealthTimer -= decreaseRate * Time.deltaTime; 
            
            SaveHealth(Mathf.Max(0, Mathf.CeilToInt(currentHealthTimer)));
            UpdateHealthUI();

            if (currentHealthTimer <= 0) Die();
        }
    }



    // 4. EL TRADUCTOR VISUAL (Dibuja las barras y los textos)
    public void UpdateHealthUI() 
    {
        if (RE_GameProgress.Instance == null) return;

        int current = RE_GameProgress.Instance.progressData.RE_PlayerHealth; 
        int max = GetMaxHealth();
        float percent = (float)current / max; 

        if (healthSlider != null) { healthSlider.maxValue = max; healthSlider.value = current; }
        if (healthImageFill != null) healthImageFill.fillAmount = percent;

        if (healthText != null)
        {
            if (useAsTimer) 
            {
                int min = Mathf.FloorToInt(currentHealthTimer / 60f);
                int sec = Mathf.FloorToInt(currentHealthTimer % 60f);
                healthText.text = string.Format("Tiempo: {0:00}:{1:00}", min, sec); 
            }
            else healthText.text = "Salud";
        }
    }

    // 5. LLAMAR A LA AMBULANCIA (Muerte)
    private void Die() 
    {
        // Resucitamos al jugador por dentro para que al reiniciar nivel empiece sano.
        SaveHealth(GetMaxHealth()); 
        
        // Saltamos a la pantalla de derrota
        if (RE_LevelTransitionManager.Instance != null) RE_LevelTransitionManager.Instance.TransitionToScene(gameOverSceneName);
        else SceneManager.LoadScene(gameOverSceneName);
    }

    // --- FUNCIONES MÁGICAS DE AYUDA (Para no repetir código) ---
    private int GetMaxHealth() 
    {
        if (RE_GameProgress.Instance == null) return 100;
        int max = RE_GameProgress.Instance.progressData.playerMaxHealth;
        return max > 0 ? max : 100;
    }

    private void SaveHealth(int amount)
    {
        if (RE_GameProgress.Instance != null)
        {
            RE_GameProgress.Instance.progressData.RE_PlayerHealth = amount;
            RE_GameProgress.Instance.SaveProgress();
        }
    }
}
