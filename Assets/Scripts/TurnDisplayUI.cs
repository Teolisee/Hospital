using TMPro;
using UnityEngine;

/// <summary>Cartel de turnos: "Atendiendo: A-003 | Tu turno: A-007 (4 antes)".</summary>
public class TurnDisplayUI : MonoBehaviour
{
    [SerializeField] private TMP_Text servingText;
    [SerializeField] private TMP_Text yourTurnText;

    private void Start()
    {
        var tm = TurnManager.Instance;
        tm.OnServingChanged += _ => Refresh();
        tm.OnPlayerTurnIssued += _ => Refresh();
        Refresh();
    }

    private void Refresh()
    {
        var tm = TurnManager.Instance;
        servingText.text = $"Atendiendo: {tm.Format(tm.CurrentlyServing)}";
        yourTurnText.text = tm.PlayerHasTurn
            ? $"Tu turno: {tm.Format(tm.PlayerTurn)} ({tm.PeopleAhead()} antes)"
            : "Aún no tienes turno";
    }
}