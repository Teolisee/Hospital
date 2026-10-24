/// <summary>
/// Contrato base que deben implementar todos los objetos interactuables del juego (puertas, NPCs, objetos recogibles).
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Devuelve el texto o aviso flotante que se mostrará al jugador al aproximarse al objeto.
    /// </summary>
    string GetInteractPrompt();

    /// <summary>
    /// Ejecuta la acción lógica asociada al objeto al presionar la tecla de interacción.
    /// </summary>
    void Interact();
}
