using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controla el Menú Principal, inicializa el cursor y el tiempo,
/// y arranca una partida nueva asegurando una limpieza completa de datos.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Paneles de la UI")]
    [Tooltip("Panel que contiene las instrucciones del juego (opcional).")]
    [SerializeField] private GameObject instructionsPanel;

    [Header("Configuración de Nivel")]
    [Tooltip("Nombre de la escena del primer nivel a cargar.")]
    [SerializeField] private string firstLevelSceneName = "Nivel 1";

    private void Start()
    {
        // Garantizar tiempo normal y cursor disponible en el menú
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (instructionsPanel != null)
        {
            instructionsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Limpia todos los datos guardados e inicia el primer nivel.
    /// </summary>
    public void PlayGame()
    {
        Time.timeScale = 1f;

        // Limpieza profunda de datos previos para iniciar una partida fresca
        PlayerPrefs.DeleteKey("HospitalGameProgress");
        PlayerPrefs.DeleteKey("HospitalGam");
        PlayerPrefs.Save();

        if (RE_GameProgress.Instance != null)
        {
            RE_GameProgress.Instance.ResetProgress();
        }

        string targetScene = !string.IsNullOrEmpty(firstLevelSceneName) ? firstLevelSceneName : "Nivel 1";

        if (RE_LevelTransitionManager.Instance != null)
        {
            RE_LevelTransitionManager.Instance.TransitionToScene(targetScene);
        }
        else
        {
            SceneManager.LoadScene(RE_LevelTransitionManager.ResolveValidSceneName(targetScene));
        }
    }

    public void ToggleInstructions(bool show)
    {
        if (instructionsPanel != null)
        {
            instructionsPanel.SetActive(show);
        }
    }

    public void QuitGame()
    {
        Debug.Log("[MainMenu] Cerrando aplicación...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
