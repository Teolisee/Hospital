using System;
using UnityEngine;

/// <summary>
/// Gestiona la cola de turnos del hospital. Es independiente del resto del juego:
/// nadie necesita modificarse, solo escuchar sus eventos.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private string prefix = "A";
    [SerializeField] private Vector2Int npcsAheadRange = new Vector2Int(3, 6); // pacientes que ya estaban esperando
    [SerializeField] private float secondsPerTurn = 15f;                      // cuánto tarda en avanzar la cola

    public int CurrentlyServing { get; private set; } = 1;
    public int LastIssued { get; private set; }
    public int PlayerTurn { get; private set; } = -1;
    public bool PlayerHasTurn => PlayerTurn > 0;
    public bool PlayerWasCalled { get; private set; }

    public event Action<int> OnPlayerTurnIssued;   // número asignado al jugador
    public event Action<int> OnServingChanged;     // turno que se está atendiendo ahora
    public event Action OnPlayerTurnCalled;        // le toca al jugador

    private float timer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Simula NPCs que ya tenían turno antes de que llegue el jugador
        LastIssued = CurrentlyServing + UnityEngine.Random.Range(npcsAheadRange.x, npcsAheadRange.y + 1);
    }

    private void Update()
    {
        if (PlayerWasCalled || CurrentlyServing >= LastIssued) return;

        timer += Time.deltaTime;
        if (timer < secondsPerTurn) return;
        timer = 0f;

        CurrentlyServing++;
        OnServingChanged?.Invoke(CurrentlyServing);

        if (PlayerHasTurn && CurrentlyServing >= PlayerTurn)
        {
            PlayerWasCalled = true;
            OnPlayerTurnCalled?.Invoke();
        }
    }

    /// <summary>Llamar cuando el jugador gana el minijuego.</summary>
    public int IssuePlayerTurn()
    {
        if (PlayerHasTurn) return PlayerTurn;
        LastIssued++;
        PlayerTurn = LastIssued;
        OnPlayerTurnIssued?.Invoke(PlayerTurn);
        return PlayerTurn;
    }

    public int PeopleAhead() => PlayerHasTurn ? Mathf.Max(0, PlayerTurn - CurrentlyServing) : 0;
    public string Format(int n) => $"{prefix}-{n:000}";
}