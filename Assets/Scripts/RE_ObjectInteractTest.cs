using UnityEngine;

/// <summary>
/// Objeto de prueba para validar el sistema de interacción con IInteractable.
/// La interacción es despachada por RE_PlayerInteraction cuando el jugador presiona 'E'.
/// </summary>
public class RE_ObjectInteractTest : MonoBehaviour, IInteractable 
{
    [Header("Configuración de Interacción")]
    [Tooltip("Distancia máxima recomendada de interacción.")]
    public float interactionDistance = 3.5f;

    [Tooltip("Mensaje flotante que aparecerá al mirar este objeto.")]
    public string promptMessage = "Presiona E para interactuar";

    [Tooltip("Mensaje personalizado que se imprimirá en consola al interactuar.")]
    public string customMessage = "¡Has interactuado con este objeto correctamente!";

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
