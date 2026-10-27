using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Se añade a la recepcionista SIN modificar tu flujo actual.
/// Llama a Interact() desde donde ya detectas la interacción con ella
/// (o desde un UnityEvent / OnTriggerEnter).
/// </summary>
public class ReceptionistTurnHook : MonoBehaviour
{
    [SerializeField] private BruteforceMinigame minigame;

    [Header("Eventos")]
    public UnityEvent onAlreadyHasTurn;
    public UnityEvent<string> onTurnGranted;   // texto del ticket, p. ej. "A-007"
    public UnityEvent onMinigameFailed;

    public void Interact()
    {
        var tm = TurnManager.Instance;
        if (tm == null) { Debug.LogWarning("Falta un TurnManager en la escena."); return; }

        if (tm.PlayerHasTurn) { onAlreadyHasTurn?.Invoke(); return; }

        var bm = minigame != null ? minigame : BruteforceMinigame.Instance;
        if (bm == null) bm = FindFirstObjectByType<BruteforceMinigame>(FindObjectsInactive.Include);
        if (bm == null) { Debug.LogWarning("No se encontró ningún BruteforceMinigame en la escena."); return; }

        bm.Open(success =>
        {
            if (success)
            {
                int n = tm.IssuePlayerTurn();
                onTurnGranted?.Invoke(tm.Format(n));
            }
            else onMinigameFailed?.Invoke();
        });
    }
}
