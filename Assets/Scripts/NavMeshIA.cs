using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class NpcRutina : MonoBehaviour
{
    [Header("Puntos de destino (arrastra objetos vacíos del piso aquí)")]
    public Transform[] puntos;

    [Header("Configuración")]
    public float tiempoEspera = 2f; // cuánto espera el NPC en cada punto

    [Header("Animación")]
    [Tooltip("Nombre del parámetro Float en el Animator que controla Idle/Walk")]
    public string parametroVelocidad = "Speed";
    [Tooltip("Suaviza la transición de velocidad en el Animator")]
    public float suavizadoAnimacion = 0.15f;

    private NavMeshAgent agent;
    private Animator animator;
    private int indice = 0;
    private bool esperando = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        // Rotación controlada por el agente, solo en el eje Y (nunca mirando arriba/abajo)
        agent.updateRotation = true;
        agent.updateUpAxis = false;

        // MUY IMPORTANTE: el Animator NO debe mover al personaje,
        // eso ya lo hace el NavMeshAgent. Si dejas Root Motion activo,
        // se pelean entre sí y el NPC se queda trabado o patina.
        animator.applyRootMotion = false;

        if (puntos.Length > 0)
            IrAlSiguientePunto();
        else
            Debug.LogWarning("No hay puntos asignados en " + gameObject.name);
    }

    void Update()
    {
        SincronizarAnimacion();

        if (esperando) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f)
            {
                esperando = true;
                Invoke(nameof(IrAlSiguientePunto), tiempoEspera);
            }
        }
    }

    void SincronizarAnimacion()
    {
        // Velocidad real del agente (0 = quieto, mayor = caminando/corriendo)
        float velocidadActual = agent.velocity.magnitude;

        // SetFloat con suavizado para que la transición Idle <-> Walk no sea brusca
        animator.SetFloat(parametroVelocidad, velocidadActual, suavizadoAnimacion, Time.deltaTime);
    }

    void IrAlSiguientePunto()
    {
        esperando = false;

        if (puntos.Length == 0) return;

        agent.SetDestination(puntos[indice].position);
        indice = (indice + 1) % puntos.Length;
    }
}