using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Controla el sistema de pausa del juego mediante las teclas 'Escape' o 'P',
/// gestiona el tiempo (Time.timeScale), el cursor del ratón y la navegación segura desde la pausa.
/// </summary>
public class MenuPausa : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("Panel de la interfaz gráfica que se muestra al pausar.")]
    public GameObject panelPause;

    [Header("Estado")]
    public bool pause = false;

    private void Start()
    {
        // Asegurar que al comenzar la escena el juego no esté congelado
        Time.timeScale = 1f;
        if (panelPause != null)
        {
            panelPause.SetActive(false);
        }
        pause = false;
    }

    private void Update()
    {
        // Si hay un diálogo de NPC abierto, no interferir con la pausa
        if (RE_NPCInteraction.IsAnyDialogueOpen) return;

        if (WasPauseTogglePressed())
        {
            if (!pause)
            {
                Pausar();
            }
            else
            {
                Resume();
            }
        }
    }

    private bool WasPauseTogglePressed()
    {
        // Soporte Input System moderno
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
        {
            return true;
        }

        // Fallback Entrada tradicional
        try
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                return true;
            }
        }
        catch { }

        return false;
    }

    public void Pausar()
    {
        if (panelPause != null) panelPause.SetActive(true);
        pause = true;
        Time.timeScale = 0f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        if (panelPause != null) panelPause.SetActive(false);
        pause = false;
        Time.timeScale = 1f;

        // Solo bloquear el ratón si no hay diálogo activo
        if (!RE_NPCInteraction.IsAnyDialogueOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>
    /// Reinicia el nivel actual de forma segura restaurando el tiempo a 1.
    /// </summary>
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        pause = false;

        if (RE_GameProgress.Instance != null)
        {
            RE_GameProgress.Instance.ResetLevelProgressOnly();
        }

        string currentScene = SceneManager.GetActiveScene().name;
        if (RE_LevelTransitionManager.Instance != null)
        {
            RE_LevelTransitionManager.Instance.TransitionToScene(currentScene);
        }
        else
        {
            SceneManager.LoadScene(currentScene);
        }
    }

    /// <summary>
    /// Vuelve al menú principal restaurando el tiempo a 1.
    /// </summary>
    public void LoadMainMenu(string sceneName = "MainMenu")
    {
        Time.timeScale = 1f;
        pause = false;

        string target = !string.IsNullOrEmpty(sceneName) && sceneName != "MenuPrincipal" ? sceneName : "MainMenu";

        if (RE_LevelTransitionManager.Instance != null)
        {
            RE_LevelTransitionManager.Instance.TransitionToScene(target);
        }
        else
        {
            SceneManager.LoadScene(RE_LevelTransitionManager.ResolveValidSceneName(target));
        }
    }

    private void OnDisable()
    {
        // Prevenir que la escena quede congelada si se desactiva este componente
        if (pause)
        {
            Time.timeScale = 1f;
        }
    }
}