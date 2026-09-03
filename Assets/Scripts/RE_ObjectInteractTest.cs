using UnityEngine; // Funciones básicas de Unity
using UnityEngine.InputSystem; // Sistema moderno para detectar controles y teclado

// -----------------------------------------------------------------------------
// SCRIPT: RE_ObjectInteractTest
// METÁFORA: "El Objeto de Pruebas"
// Implementa IInteractable para que el jugador pueda interactuar con él usando la E.
// -----------------------------------------------------------------------------
public class RE_ObjectInteractTest : MonoBehaviour, IInteractable 
{
    [Header("Configuración de Interacción")]
    [Tooltip("Distancia máxima a la que debe estar el jugador para interactuar.")]
    public float interactionDistance = 3.5f;

    [Tooltip("Mensaje flotante que aparecerá al acercarse.")]
    public string promptMessage = "Presiona E para interactuar";

    [Tooltip("Mensaje personalizado que se imprimirá en la consola.")]
    public string customMessage = "¡Has interactuado con este objeto correctamente!";

    private Transform playerTransform;

    void Start()
    {
        BuscarJugador();
    }

    private void BuscarJugador()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            RE_PlayerMovement pm = FindFirstObjectByType<RE_PlayerMovement>();
            if (pm != null) player = pm.gameObject;
        }

        if (player != null) playerTransform = player.transform;
    }

    void Update()
    {
        if (playerTransform == null)
        {
            BuscarJugador();
            return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        if (distance <= interactionDistance)
        {
            if (WasInteractPressed())
            {
                Interact();
            }
        }
    }

    private bool WasInteractPressed()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame) return true;
        
        try
        {
            if (Input.GetKeyDown(KeyCode.E)) return true;
        }
        catch { }

        return false;
    }

    // Métodos de la interfaz IInteractable
    public string GetInteractPrompt()
    {
        return !string.IsNullOrEmpty(promptMessage) ? promptMessage : $"Presiona E para interactuar con {gameObject.name}";
    }

    public void Interact()
    {
        Debug.Log($"[RE_ObjectInteractTest] [{gameObject.name}] Interactuado: {customMessage}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
